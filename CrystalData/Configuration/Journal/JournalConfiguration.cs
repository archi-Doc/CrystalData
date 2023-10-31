// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

[TinyhandUnion("EmptyJournal", typeof(EmptyJournalConfiguration))]
[TinyhandUnion("SimpleJournal", typeof(SimpleJournalConfiguration))]
public abstract partial record JournalConfiguration
{
    public JournalConfiguration()
    {
    }
}
