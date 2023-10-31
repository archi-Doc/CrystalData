// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Journal;

public partial class SimpleJournal
{
    private class SimpleJournalTask : TaskCore
    {
        public SimpleJournalTask(SimpleJournal simpleJournal)
            : base(null, Process)
        {
            this.simpleJournal = simpleJournal;
        }

        private static async Task Process(object? parameter)
        {
            var core = (SimpleJournalTask)parameter!;
            while (await core.Delay(core.simpleJournal.SimpleJournalConfiguration.SaveIntervalInMilliseconds).ConfigureAwait(false))
            {
                await core.simpleJournal.SaveJournalAsync(true).ConfigureAwait(false);
            }

            await core.simpleJournal.SaveJournalAsync(false).ConfigureAwait(false);
        }

        private SimpleJournal simpleJournal;
    }
}
