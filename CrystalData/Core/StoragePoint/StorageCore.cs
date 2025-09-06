// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public partial class StorageControl
{
    private const int IntervalInMilliseconds = 100;

    private class StorageCore : TaskCore
    {
        private readonly StorageControl storageControl;

        public StorageCore(StorageControl storageControl)
            : base(null, Process, false)
        {
            this.storageControl = storageControl;
        }

        private static async Task Process(object? parameter)
        {
            var core = (StorageCore)parameter!;
            var storageControl = core.storageControl;

            while (!core.IsTerminated)
            {
                var delayFlag = true;

                if (storageControl.StorageReleaseRequired)
                {
                    await storageControl.ReleaseStorage(core.CancellationToken);
                    delayFlag = false;
                }

                if (await storageControl.ProcessSaveQueue(core.CancellationToken))
                {
                    delayFlag = false;
                }

                if (delayFlag)
                {
                    await core.Delay(IntervalInMilliseconds);
                }
            }
        }
    }
}
