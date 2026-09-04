// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Provides the base configuration for a crystal journal.
/// </summary>
[TinyhandUnion("EmptyJournal", typeof(EmptyJournalConfiguration))]
[TinyhandUnion("SimpleJournal", typeof(SimpleJournalConfiguration))]
public abstract partial record JournalConfiguration
{
    public JournalConfiguration()
    {
    }
}
