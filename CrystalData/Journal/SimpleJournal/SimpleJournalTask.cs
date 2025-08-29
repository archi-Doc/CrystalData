// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Journal;

public partial class SimpleJournal
{
    private class SimpleJournalTask : TaskCore
    {
        public SimpleJournalTask(SimpleJournal simpleJournal)
            : base(null, Process, false)
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

        private SimpleJournal simpleJournal;
    }
}
