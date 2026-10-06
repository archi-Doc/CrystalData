// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1124 // Do not use regions

namespace CrystalData.Filer;

/// <summary>
/// Provides queued, path-serialized execution for filer implementations.
/// </summary>
public abstract class FilerBase : ReusableJobWorker<FilerWork>, IFiler
{
    public const int DefaultConcurrentTasks = 4;

    public FilerBase(ExecutionRoot root, int poolCapacity = 32)
        : this(root, poolCapacity, StringComparer.Ordinal)
    {
    }

    protected FilerBase(ExecutionRoot root, int poolCapacity, StringComparer pathComparer)
        : base(root.GetCrystalDataGroup(), null, poolCapacity)
    {
        this.MaxConcurrentTasks = DefaultConcurrentTasks;
        this.pathToLastWork = new(pathComparer);
    }

    public override string ToString()
        => $"FilerBase";

    #region FieldAndProperty

    bool IFiler.SupportsPartialWrite => true;

    protected CrystalControl? CrystalControl { get; set; }

    private readonly Lock queueLock = new();
    private readonly Dictionary<string, FilerWork> pathToLastWork;

    #endregion

    /// <summary>
    /// Enqueues work in submission order for its path, including work waiting behind an active operation.
    /// </summary>
    /// <param name="work">The initialized job.</param>
    /// <returns>A completed task once the job has been queued.</returns>
    public new Task Add(FilerWork work)
    {
        using (this.queueLock.EnterScope())
        {
            if (work.IsQueued || work.State != ReusableJobState.Initial)
            {
                throw new InvalidOperationException("A filer job can only be queued once before it is returned and initialized again.");
            }

            work.QueuePath = this.GetQueuePath(work.Path);
            work.IsQueued = true;
            if (this.pathToLastWork.TryGetValue(work.QueuePath, out var previous))
            {
                previous.NextWork = work;
                this.pathToLastWork[work.QueuePath] = work;
            }
            else
            {
                this.pathToLastWork.Add(work.QueuePath, work);
                base.Add(work);
            }
        }

        return Task.CompletedTask;
    }

    async Task<CrystalResult> IFiler.PrepareAndCheck(PrepareParam param, PathConfiguration configuration)
    {
        throw new NotImplementedException();
    }

    async Task IFiler.FlushAsync(bool terminate)
    {
        await this.WaitForCompletionAsync().ConfigureAwait(false);
        if (terminate)
        {
            this.Dispose();
        }
    }

    CrystalResult IFiler.WriteAndForget(string path, long offset, BytePool.RentedReadOnlyMemory dataToBeShared, bool truncate)
    {
        if (!((IFiler)this).SupportsPartialWrite && (offset != 0 || !truncate))
        {// Not supported
            return CrystalResult.NoPartialWriteSupport;
        }

        var job = this.Rent(ReusableJobOptions.ReturnToPoolOnCompletion);
        job.Initialize(path, offset, dataToBeShared, truncate);
        _ = this.Add(job);
        return CrystalResult.Started;
    }

    CrystalResult IFiler.DeleteAndForget(string path)
    {
        var job = this.Rent(ReusableJobOptions.ReturnToPoolOnCompletion);
        job.Initialize(FilerWork.WorkType.Delete, path);
        _ = this.Add(job);
        return CrystalResult.Started;
    }

    async Task<CrystalMemoryOwnerResult> IFiler.ReadAsync(string path, long offset, int length, TimeSpan timeToWait)
    {
        var job = this.Rent();
        job.Initialize(path, offset, length);
        await this.Add(job).ConfigureAwait(false);
        if (!await this.WaitForJobAsync(job, timeToWait).ConfigureAwait(false))
        {
            return new(CrystalResult.Aborted); // Timeout
        }

        var result = new CrystalMemoryOwnerResult(job.Result, job.ReadData.ReadOnly);
        job.ReadData = default; // Transfer ownership to the caller before reusing the job.
        this.Return(job);
        return result;
    }

    async Task<CrystalResult> IFiler.WriteAsync(string path, long offset, BytePool.RentedReadOnlyMemory dataToBeShared, TimeSpan timeToWait, bool truncate)
    {
        if (!((IFiler)this).SupportsPartialWrite && (offset != 0 || !truncate))
        {// Not supported
            return CrystalResult.NoPartialWriteSupport;
        }

        var job = this.Rent();
        job.Initialize(path, offset, dataToBeShared, truncate);
        await this.Add(job).ConfigureAwait(false);
        return await this.WaitForResultAsync(job, timeToWait).ConfigureAwait(false);
    }

    async Task<CrystalResult> IFiler.DeleteAsync(string path, TimeSpan timeToWait)
    {
        var job = this.Rent();
        job.Initialize(FilerWork.WorkType.Delete, path);
        await this.Add(job).ConfigureAwait(false);
        return await this.WaitForResultAsync(job, timeToWait).ConfigureAwait(false);
    }

    async Task<CrystalResult> IFiler.DeleteDirectoryAsync(string path, bool recursive, TimeSpan timeToWait)
    {
        var workType = recursive ? FilerWork.WorkType.DeleteDirectory : FilerWork.WorkType.DeleteEmptyDirectory;
        var job = this.Rent();
        job.Initialize(workType, path);
        await this.Add(job).ConfigureAwait(false);
        return await this.WaitForResultAsync(job, timeToWait).ConfigureAwait(false);
    }

    async Task<List<PathInformation>> IFiler.ListAsync(string path, TimeSpan timeToWait)
    {
        var job = this.Rent();
        job.Initialize(FilerWork.WorkType.List, path);
        await this.Add(job).ConfigureAwait(false);
        if (!await this.WaitForJobAsync(job, timeToWait).ConfigureAwait(false))
        {
            return new();
        }

        var result = job.OutputObject as List<PathInformation> ?? new();
        job.OutputObject = null;
        this.Return(job);
        return result;
    }

    /// <summary>
    /// Gets the identity used to serialize operations on one path.
    /// </summary>
    /// <param name="path">The requested path.</param>
    /// <returns>The path identity.</returns>
    protected virtual string GetQueuePath(string path)
        => path;

    protected override void OnJobFinished(FilerWork job)
    {
        job.ReturnWriteData(); // Also release writes aborted before their processor starts.
        if (job.State == ReusableJobState.Aborted)
        {
            job.Result = CrystalResult.Aborted;
        }

        using (this.queueLock.EnterScope())
        {
            var next = job.NextWork;
            job.NextWork = null;
            if (next is null)
            {
                this.pathToLastWork.Remove(job.QueuePath);
            }
            else if (!this.CanContinue)
            {
                this.pathToLastWork.Remove(job.QueuePath);
                while (next is not null)
                {// Aborting a long queue must not recursively finish its entire chain.
                    var following = next.NextWork;
                    next.NextWork = null;
                    base.Add(next);
                    next = following;
                }
            }
            else
            {
                base.Add(next); // Keep an outstanding worker job until the entire path queue is drained.
            }
        }
    }

    private async Task<CrystalResult> WaitForResultAsync(FilerWork job, TimeSpan timeout)
    {
        if (!await this.WaitForJobAsync(job, timeout).ConfigureAwait(false))
        {
            return CrystalResult.Aborted;
        }

        var result = job.Result;
        this.Return(job);
        return result;
    }

    private async Task<bool> WaitForJobAsync(FilerWork job, TimeSpan timeout)
    {
        try
        {
            var wait = job.WaitAsync(timeout);
            await wait.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            if (wait.IsCompletedSuccessfully)
            {
                return true;
            }
        }
        catch
        {
            _ = this.ReturnAbandonedJobAsync(job);
            throw;
        }

        _ = this.ReturnAbandonedJobAsync(job);
        return false;
    }

    private async Task ReturnAbandonedJobAsync(FilerWork job)
    {
        await job.Task.ConfigureAwait(false);
        job.ReadData = job.ReadData.Return();
        job.OutputObject = null;
        this.Return(job);
    }
}
