// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Unload;

internal static class UnloadTaskExtension
{
    public static async Task UnloadTask(Crystalizer crystalizer, UnloadTask.GoshujinClass goshujin)
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

    public static async Task<(int Unloaded, int Remaining)> ProcessGoshujin(Crystalizer crystalizer, UnloadTask.GoshujinClass goshujin)
    {
        var unloaded = 0;
        UnloadTask? task;
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
                await task.Crystal.Save(UnloadMode.ForceUnload).ConfigureAwait(false);
                crystalizer.Logger.TryGet(LogLevel.Error)?.Log(CrystalDataHashed.Unload.ForceUnloaded, task.Crystal.DataType.FullName!);
                unloaded++;
            }
            else
            {// Try
                var result = await task.Crystal.Save(UnloadMode.TryUnload).ConfigureAwait(false);
                if (result == CrystalResult.DataIsLocked)
                {
                    crystalizer.Logger.TryGet(LogLevel.Warning)?.Log(CrystalDataHashed.Unload.Locked, task.Crystal.DataType.FullName!);
                    task.GoshujinSemaphore?.LockAndForceUnload();
                    using (goshujin.LockObject.EnterScope())
                    {
                        task.LastProcessed = utc;
                        task.Goshujin = goshujin;
                    }
                }
                else
                {
                    crystalizer.Logger.TryGet(LogLevel.Information)?.Log(CrystalDataHashed.Unload.Unloaded, task.Crystal.DataType.FullName!);
                    unloaded++;
                }
            }
        }
    }
}

[ValueLinkObject(Isolation = IsolationLevel.Serializable)]
internal partial class UnloadTask : IEquatable<UnloadTask>
{
    public UnloadTask(ICrystal crystal)
    {
        this.Crystal = crystal;
        this.GoshujinSemaphore = crystal as IGoshujinSemaphore;
    }

    #region FieldAndProperty

    public ICrystal Crystal { get; }

    public IGoshujinSemaphore? GoshujinSemaphore { get; }

    public DateTime FirstProcessed { get; set; }

    [Link(Type = ChainType.Ordered)]
    public DateTime LastProcessed { get; set; }

    #endregion

    public override int GetHashCode() => this.Crystal.GetHashCode();

    public bool Equals(UnloadTask? other)
    {
        if (other == null)
        {
            return false;
        }

        return this.Crystal == other.Crystal;
    }
}
