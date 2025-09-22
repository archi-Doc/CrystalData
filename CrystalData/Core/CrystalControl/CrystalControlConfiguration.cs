// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public record class CrystalControlConfiguration
{
    public CrystalControlConfiguration()
    {
    }

    public Dictionary<Type, CrystalConfiguration> CrystalConfigurations { get; init; } = new();

    public JournalConfiguration JournalConfiguration { get; init; } = EmptyJournalConfiguration.Default;
}
