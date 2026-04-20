// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1124 // Do not use regions

namespace CrystalData.Filer;

public abstract class FilerBase : ReusableJobWorker<FilerWork>, IFiler
{
    public const int DefaultConcurrentTasks = 4;

    public FilerBase(WorkDelegate process)
        : base(null, process, true)
    {
        this.NumberOfConcurrentTasks = DefaultConcurrentTasks;
        this.SetCanStartConcurrentlyDelegate((workInterface, workingList) =>
        {// Lock IO order
            var path = workInterface.Work.Path;
            foreach (var x in workingList)
            {
                if (x.Work.Path == path)
                {
                    return false;
                }
            }

            return true;
        });
    }

    public override string ToString()
        => $"FilerBase";

    #region FieldAndProperty

    bool IFiler.SupportPartialWrite => true;

    protected CrystalControl? CrystalControl { get; set; }

    #endregion

    async Task<CrystalResult> IFiler.PrepareAndCheck(PrepareParam param, PathConfiguration configuration)
    {
        throw new NotImplementedException();
    }

    async Task IFiler.FlushAsync(bool terminate)
    {
        await this.WaitForCompletion(-1).ConfigureAwait(false);
        if (terminate)
        {
            this.Dispose();
        }
    }

    CrystalResult IFiler.WriteAndForget(string path, long offset, BytePool.RentReadOnlyMemory dataToBeShared, bool truncate)
    {
        if (!((IFiler)this).SupportPartialWrite && (offset != 0 || !truncate))
        {// Not supported
            return CrystalResult.NoPartialWriteSupport;
        }

        var job = this.Rent(true);
        job.Initialize(path, offset, dataToBeShared, truncate);
        this.Add(job);
        return CrystalResult.Started;
    }

    CrystalResult IFiler.DeleteAndForget(string path)
    {
        var job = this.Rent(true);
        job.Initialize(FilerWork.WorkType.Delete, path);
        this.Add(job);
        return CrystalResult.Started;
    }

    async Task<CrystalMemoryOwnerResult> IFiler.ReadAsync(string path, long offset, int length, TimeSpan timeToWait)
    {
        var job = this.Rent();
        job.Initialize(path, offset, length);
        this.Add(job);
        await job.Task.WaitAsync(timeToWait).ConfigureAwait(false);//
        return new(job.Result, job.ReadData.ReadOnly);
    }

    async Task<CrystalResult> IFiler.WriteAsync(string path, long offset, BytePool.RentReadOnlyMemory dataToBeShared, TimeSpan timeToWait, bool truncate)
    {
        if (!((IFiler)this).SupportPartialWrite && (offset != 0 || !truncate))
        {// Not supported
            return CrystalResult.NoPartialWriteSupport;
        }

        var job = this.Rent();
        job.Initialize(path, offset, dataToBeShared, truncate);
        this.Add(job);
        await job.WaitAsync(timeToWait).ConfigureAwait(false);
        return job.Result;
    }

    async Task<CrystalResult> IFiler.DeleteAsync(string path, TimeSpan timeToWait)
    {
        var job = this.Rent();
        job.Initialize(FilerWork.WorkType.Delete, path);
        this.Add(job);
        await job.WaitAsync(timeToWait).ConfigureAwait(false);
        return job.Result;
    }

    async Task<CrystalResult> IFiler.DeleteDirectoryAsync(string path, bool recursive, TimeSpan timeToWait)
    {
        var workType = recursive ? FilerWork.WorkType.DeleteDirectory : FilerWork.WorkType.DeleteEmptyDirectory;
        var job = this.Rent();
        job.Initialize(workType, path);
        this.Add(job);
        await job.WaitAsync(timeToWait).ConfigureAwait(false);
        return job.Result;
    }

    async Task<List<PathInformation>> IFiler.ListAsync(string path, TimeSpan timeToWait)
    {
        var job = this.Rent();
        job.Initialize(FilerWork.WorkType.List, path);
        this.Add(job);
        await job.WaitAsync(timeToWait).ConfigureAwait(false);
        if (job.OutputObject is List<PathInformation> list)
        {
            return list;
        }
        else
        {
            return new List<PathInformation>();
        }
    }
}
