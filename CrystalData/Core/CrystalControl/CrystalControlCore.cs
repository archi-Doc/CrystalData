// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData.Internal;

namespace CrystalData;

public partial class CrystalControl
{
    private class CrystalControlCore : TaskCore
    {
        private const int IntervalInMilliseconds = 100;
        private const int SaveBatchSize = 32;

        private readonly CrystalControl crystalControl;
        private readonly StorageControl storageControl;
        private StorageObject[] tempArray = new StorageObject[SaveBatchSize];
        private ICrystalInternal[] tempArray2 = new ICrystalInternal[SaveBatchSize];

        public CrystalControlCore(CrystalControl crystalControl)
            : base(null, Process, false)
        {
            this.crystalControl = crystalControl;
            this.storageControl = crystalControl.StorageControl;
        }

        private static async Task Process(object? parameter)
        {
            var core = (CrystalControlCore)parameter!;
            var crystalControl = core.crystalControl;
            var storageControl = core.storageControl;

            while (!core.IsTerminated)
            {
                var timeUpdated = crystalControl.UpdateTime();
                var delayFlag = true;

                if (storageControl.StorageReleaseRequired)
                {// Releases storage when the memory usage limit is reached.
                    await storageControl.ReleaseStorage(core.CancellationToken).ConfigureAwait(false);
                    delayFlag = false;
                }

                if (timeUpdated)
                {
                    if (await storageControl.ProcessSaveQueue(core.tempArray, crystalControl, core.CancellationToken).ConfigureAwait(false))
                    {// Processes the save queue.
                        delayFlag = false;
                    }

                    if (await crystalControl.ProcessSaveQueue(core.tempArray2, crystalControl, core.CancellationToken).ConfigureAwait(false))
                    {// Processes the save queue.
                        delayFlag = false;
                    }
                }

                if (delayFlag)
                {
                    await core.Delay(IntervalInMilliseconds).ConfigureAwait(false);
                }
            }
        }
    }
}
