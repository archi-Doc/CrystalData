// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public partial class Crystalizer
{
    private class CrystalizerTask : TaskCore
    {
        private const int TaskIntervalInMilliseconds = 1_000;
        private const int PeriodicSaveInMilliseconds = 10_000;

        private readonly Crystalizer crystalizer;

        public CrystalizerTask(Crystalizer crystalizer)
            : base(null, Process)
        {
            this.crystalizer = crystalizer;
        }

        private static async Task Process(object? parameter)
        {
            var core = (CrystalizerTask)parameter!;
            int elapsedMilliseconds = 0;
            while (await core.Delay(TaskIntervalInMilliseconds).ConfigureAwait(false))
            {
                await core.crystalizer.QueuedStore().ConfigureAwait(false);

                elapsedMilliseconds += TaskIntervalInMilliseconds;
                if (elapsedMilliseconds >= PeriodicSaveInMilliseconds)
                {
                    elapsedMilliseconds = 0;
                    await core.crystalizer.PeriodicStore().ConfigureAwait(false);
                }
            }
        }
    }
}
