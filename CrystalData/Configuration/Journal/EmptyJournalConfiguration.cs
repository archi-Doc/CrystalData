// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

[TinyhandObject]
public partial record EmptyJournalConfiguration : JournalConfiguration
{
    public static readonly EmptyJournalConfiguration Default = new();

    public EmptyJournalConfiguration()
        : base()
    {
    }
}
