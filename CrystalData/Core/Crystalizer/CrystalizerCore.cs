// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData.Internal;

namespace CrystalData;

public partial class Crystalizer
{
    private class CrystalizerCore : TaskCore
    {
        private const int IntervalInMilliseconds = 100;
        private const int SaveBatchSize = 32;

        private readonly Crystalizer crystalizer;
        private readonly StorageControl storageControl;
        private StorageObject[] tempArray = new StorageObject[SaveBatchSize];
        private ICrystalInternal[] tempArray2 = new ICrystalInternal[SaveBatchSize];

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
                    if (await storageControl.ProcessSaveQueue(core.tempArray, crystalizer, core.CancellationToken))
                    {// Processes the save queue.
                        delayFlag = false;
                    }

                    if (await crystalizer.ProcessSaveQueue(core.tempArray2, crystalizer, core.CancellationToken))
                    {// Processes the save queue.
                        delayFlag = false;
                    }
                }

                if (delayFlag)
                {
                    await core.Delay(IntervalInMilliseconds);
                }
            }
        }
    }
}
