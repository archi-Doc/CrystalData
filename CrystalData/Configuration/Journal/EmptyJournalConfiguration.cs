// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Disables journal persistence.
/// </summary>
[TinyhandObject]
public partial record EmptyJournalConfiguration : JournalConfiguration
{
    public static readonly EmptyJournalConfiguration Default = new();

    public EmptyJournalConfiguration()
        : base()
    {
    }
}
