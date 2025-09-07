// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public partial class Crystalizer
{
    private class CrystalizerCore : TaskCore
    {
        private const int IntervalInMilliseconds = 100;

        private readonly Crystalizer crystalizer;
        private readonly StorageControl storageControl;

        public CrystalizerCore(Crystalizer crystalizer)
            : base(null, Process, false)
        {
            this.crystalizer = crystalizer;
            this.storageControl = crystalizer.StorageControl;
        }

        private static async Task Process(object? parameter)
        {
            var core = (CrystalizerCore)parameter!;
            var crystalizer = core.crystalizer;
            var storageControl = core.storageControl;

            while (!core.IsTerminated)
            {
                var timeUpdated = crystalizer.UpdateTime();
                var delayFlag = true;

                if (storageControl.StorageReleaseRequired)
                {// Releases storage when the memory usage limit is reached.
                    await storageControl.ReleaseStorage(core.CancellationToken);
                    delayFlag = false;
                }

                if (timeUpdated)
                {
                    if (await storageControl.ProcessSaveQueue(crystalizer, core.CancellationToken))
                    {// Processes the save queue.
                        delayFlag = false;
                    }

                    //
                    /*await core.crystalizer.QueuedStore().ConfigureAwait(false);
                    elapsedMilliseconds += TaskIntervalInMilliseconds;
                    if (elapsedMilliseconds >= PeriodicSaveInMilliseconds)
                    {
                        elapsedMilliseconds = 0;
                        await core.crystalizer.PeriodicStore().ConfigureAwait(false);
                    }*/
                }

                if (delayFlag)
                {
                    await core.Delay(IntervalInMilliseconds);
                }
            }
        }
    }
}
