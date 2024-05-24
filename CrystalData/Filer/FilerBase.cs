// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1124 // Do not use regions

namespace CrystalData.Filer;

public abstract class FilerBase : TaskWorker<FilerWork>, IRawFiler
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

    bool IRawFiler.SupportPartialWrite => true;

    protected Crystalizer? Crystalizer { get; set; }

    #endregion

    async Task<CrystalResult> IRawFiler.PrepareAndCheck(PrepareParam param, PathConfiguration configuration)
    {
        throw new NotImplementedException();
    }

    async Task IRawFiler.TerminateAsync()
    {
        await this.WaitForCompletionAsync().ConfigureAwait(false);
        this.Dispose();
    }

    CrystalResult IRawFiler.WriteAndForget(string path, long offset, BytePool.RentReadOnlyMemory dataToBeShared, bool truncate)
    {
        if (!((IRawFiler)this).SupportPartialWrite && (offset != 0 || !truncate))
        {// Not supported
            return CrystalResult.NoPartialWriteSupport;
        }

        this.AddLast(new(path, offset, dataToBeShared, truncate));
        return CrystalResult.Started;
    }

    CrystalResult IRawFiler.DeleteAndForget(string path)
    {
        this.AddLast(new(FilerWork.WorkType.Delete, path));
        return CrystalResult.Started;
    }

    async Task<CrystalMemoryOwnerResult> IRawFiler.ReadAsync(string path, long offset, int length, TimeSpan timeToWait)
    {
        var work = new FilerWork(path, offset, length);
        var workInterface = this.AddLast(work);
        await workInterface.WaitForCompletionAsync(timeToWait).ConfigureAwait(false);
        return new(work.Result, work.ReadData.ReadOnly);
    }

    async Task<CrystalResult> IRawFiler.WriteAsync(string path, long offset, BytePool.RentReadOnlyMemory dataToBeShared, TimeSpan timeToWait, bool truncate)
    {
        if (!((IRawFiler)this).SupportPartialWrite && (offset != 0 || !truncate))
        {// Not supported
            return CrystalResult.NoPartialWriteSupport;
        }

        var work = new FilerWork(path, offset, dataToBeShared, truncate);
        var workInterface = this.AddLast(work);
        await workInterface.WaitForCompletionAsync(timeToWait).ConfigureAwait(false);
        return work.Result;
    }

    async Task<CrystalResult> IRawFiler.DeleteAsync(string path, TimeSpan timeToWait)
    {
        var work = new FilerWork(FilerWork.WorkType.Delete, path);
        var workInterface = this.AddLast(work);
        await workInterface.WaitForCompletionAsync(timeToWait).ConfigureAwait(false);
        return work.Result;
    }

    async Task<CrystalResult> IRawFiler.DeleteDirectoryAsync(string path, TimeSpan timeToWait)
    {
        var work = new FilerWork(FilerWork.WorkType.DeleteDirectory, path);
        var workInterface = this.AddLast(work);
        await workInterface.WaitForCompletionAsync(timeToWait).ConfigureAwait(false);
        return work.Result;
    }

    async Task<List<PathInformation>> IRawFiler.ListAsync(string path, TimeSpan timeToWait)
    {
        var work = new FilerWork(FilerWork.WorkType.List, path);
        var workInterface = this.AddLast(work);
        await workInterface.WaitForCompletionAsync(timeToWait).ConfigureAwait(false);
        if (work.OutputObject is List<PathInformation> list)
        {
            return list;
        }
        else
        {
            return new List<PathInformation>();
        }
    }
}
