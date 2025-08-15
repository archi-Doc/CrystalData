// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Unload;

internal static class ReleaseTaskExtension
{
    public static async Task ReleaseTask(Crystalizer crystalizer, ReleaseTask.GoshujinClass goshujin)
    {
        while (true)
        {
            var result = await ProcessGoshujin(crystalizer, goshujin).ConfigureAwait(false);
            if (result.Remaining == 0)
            {
                return;
            }
            else if (result.Unloaded == 0)
            {
                await Task.Delay(1_000);
            }
        }
    }

    public static async Task<(int Unloaded, int Remaining)> ProcessGoshujin(Crystalizer crystalizer, ReleaseTask.GoshujinClass goshujin)
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
                {
                    return (unloaded, 0);
                }

                if ((utc - task.LastProcessed) < TimeSpan.FromSeconds(1))
                {
                    return (unloaded, goshujin.LastProcessedChain.Count);
                }

                task.Goshujin = null;
            }

            if (task.FirstProcessed == default)
            {
                task.FirstProcessed = utc;
            }

            if ((utc - task.FirstProcessed) > crystalizer.UnloadTimeout)
            {// Force
                await task.PersistableObject.Store(StoreMode.ForceRelease).ConfigureAwait(false);
                crystalizer.Logger.TryGet(LogLevel.Error)?.Log(CrystalDataHashed.Unload.ForceUnloaded, task.PersistableObject.DataType.FullName!);
                unloaded++;
            }
            else
            {// Try
                var result = await task.PersistableObject.Store(StoreMode.TryRelease).ConfigureAwait(false);
                if (result == CrystalResult.DataIsLocked)
                {
                    crystalizer.Logger.TryGet(LogLevel.Warning)?.Log(CrystalDataHashed.Unload.Locked, task.PersistableObject.DataType.FullName!);
                    task.GoshujinSemaphore?.LockAndForceRelease();
                    using (goshujin.LockObject.EnterScope())
                    {
                        task.LastProcessed = utc;
                        task.Goshujin = goshujin;
                    }
                }
                else
                {
                    crystalizer.Logger.TryGet(LogLevel.Information)?.Log(CrystalDataHashed.Unload.Unloaded, task.PersistableObject.DataType.FullName!);
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
        this.GoshujinSemaphore = persistableObject as IRepeatableSemaphore;
    }

    #region FieldAndProperty

    public IPersistable PersistableObject { get; }

    public IRepeatableSemaphore? GoshujinSemaphore { get; }

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
