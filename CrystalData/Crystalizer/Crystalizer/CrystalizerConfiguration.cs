// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public class CrystalizerConfiguration
{
    internal CrystalizerConfiguration(Dictionary<Type, CrystalConfiguration> crystalConfigurations, JournalConfiguration journalConfiguration)
    {
        this.CrystalConfigurations = crystalConfigurations;
        this.JournalConfiguration = journalConfiguration;
    }

    public Dictionary<Type, CrystalConfiguration> CrystalConfigurations { get; }

    public JournalConfiguration JournalConfiguration { get; }
}
