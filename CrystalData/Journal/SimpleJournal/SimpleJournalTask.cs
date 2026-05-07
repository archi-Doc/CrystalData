// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Journal;

public partial class SimpleJournal
{
    private class SimpleJournalTask : TaskCore
    {
        private readonly SimpleJournal simpleJournal;

        public SimpleJournalTask(ExecutionRoot root, SimpleJournal simpleJournal)
            : base(root.IndependentGroup, Process, ExecutionCoreOptions.DelayedStart)
        {
            this.simpleJournal = simpleJournal;
        }

        private static async Task Process(object? parameter)
        {
            var core = (SimpleJournalTask)parameter!;
            while (await core.Delay(core.simpleJournal.SimpleJournalConfiguration.SaveIntervalInMilliseconds).ConfigureAwait(false))
            {
                await core.simpleJournal.StoreJournalAsync(true, StoreMode.StoreOnly, default).ConfigureAwait(false);
            }

            await core.simpleJournal.StoreJournalAsync(false, StoreMode.StoreOnly, default).ConfigureAwait(false);
        }
    }
}
