// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Unload;

internal static class StoreTaskExtension
{
    private const int WaitTimeInMilliseconds = 1_000;

    public static async Task StoreTask(CrystalControl crystalControl, ReleaseTask.GoshujinClass goshujin, StoreMode storeMode)
    {
        while (true)
        {
            var result = await ProcessGoshujin(crystalControl, goshujin, storeMode).ConfigureAwait(false);
            if (result.Remaining == 0)
            {
                return;
            }
            else if (result.Unloaded == 0)
            {
                await Task.Delay(WaitTimeInMilliseconds).ConfigureAwait(false);
            }
        }
    }

    public static async Task<(int Unloaded, int Remaining)> ProcessGoshujin(CrystalControl crystalControl, ReleaseTask.GoshujinClass goshujin, StoreMode storeMode)
    {
        var unloaded = 0;
        ReleaseTask? task;
        DateTime utc;

        while (true)
        {
            utc = DateTime.UtcNow;
            using (goshujin.LockObject.EnterScope())
            {
                task = goshujin.LastProcessedChain.First;
                if (task is null)
                {// No remaining tasks.
                    return (unloaded, 0);
                }

                if ((utc - task.LastProcessed) < TimeSpan.FromMilliseconds(WaitTimeInMilliseconds))
                {
                    return (unloaded, goshujin.LastProcessedChain.Count);
                }

                task.Goshujin = null;
            }

            if (task.FirstProcessed == default)
            {
                task.FirstProcessed = utc;
            }

            if (storeMode == StoreMode.StoreOnly)
            {// Store only
                await task.PersistableObject.StoreData(StoreMode.StoreOnly).ConfigureAwait(false);
                unloaded++;
            }
            else if (storeMode == StoreMode.ForceRelease ||
                (utc - task.FirstProcessed) > crystalControl.Options.TimeoutUntilForcedRelease)
            {// Force release
                await task.PersistableObject.StoreData(StoreMode.ForceRelease).ConfigureAwait(false);
                crystalControl.Logger.TryGet(LogLevel.Error)?.Log(CrystalDataHashed.Unload.ForceUnloaded, task.PersistableObject.DataType.FullName!);
                unloaded++;
            }
            else
            {// Try release
                var result = await task.PersistableObject.StoreData(StoreMode.TryRelease).ConfigureAwait(false);
                if (result == CrystalResult.DataIsLocked)
                {
                    crystalControl.Logger.TryGet(LogLevel.Warning)?.Log(CrystalDataHashed.Unload.Locked, task.PersistableObject.DataType.FullName!);
                    task.RepeatableReadSemaphore?.LockAndForceRelease();
                    using (goshujin.LockObject.EnterScope())
                    {
                        task.LastProcessed = utc;
                        task.Goshujin = goshujin;
                    }
                }
                else
                {
                    crystalControl.Logger.TryGet(LogLevel.Information)?.Log(CrystalDataHashed.Unload.Unloaded, task.PersistableObject.DataType.FullName!);
                    unloaded++;
                }
            }
        }
    }
}

[ValueLinkObject(Isolation = IsolationLevel.Serializable)]
internal partial class ReleaseTask : IEquatable<ReleaseTask>
{
    public ReleaseTask(IPersistable persistableObject)
    {
        this.PersistableObject = persistableObject;
        this.RepeatableReadSemaphore = persistableObject as IRepeatableReadSemaphore;
    }

    #region FieldAndProperty

    public IPersistable PersistableObject { get; }

    public IRepeatableReadSemaphore? RepeatableReadSemaphore { get; }

    public DateTime FirstProcessed { get; set; }

    [Link(Type = ChainType.Ordered)]
    public DateTime LastProcessed { get; set; }

    #endregion

    public override int GetHashCode() => this.PersistableObject.GetHashCode();

    public bool Equals(ReleaseTask? other)
    {
        if (other == null)
        {
            return false;
        }

        return this.PersistableObject == other.PersistableObject;
    }
}
