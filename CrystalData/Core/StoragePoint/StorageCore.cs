using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrystalData;

public partial class StorageControl
{
    private class StorageCore : TaskCore
    {
        private const int IntervalInMilliseconds = 100;
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
                if (!storageControl.StorageReleaseRequired)
                {
                    await core.Delay(IntervalInMilliseconds);
                    continue;
                }

                IStorageData? storageData;
                using (memoryControl.lockObject.EnterScope())
                {// Get the first item.
                    if (memoryControl.items.UnloadQueueChain.TryPeek(out var item))
                    {
                        memoryControl.items.UnloadQueueChain.Remove(item);
                        memoryControl.items.UnloadQueueChain.Enqueue(item);
                        storageData = item.StorageData;
                    }
                    else
                    {// No item
                        storageData = null;
                    }
                }

                if (storageData is null)
                {// Sleep
                    await core.Delay(UnloadIntervalInMilliseconds);
                    continue;
                }

                if (await storageData.Save(UnloadMode.TryUnload))
                {// Success (deletion will be done via ReportUnload() from StorageData)
                }
                else
                {// Failure
                }
            }
        }
    }
}
