// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

internal interface ICrystalInternal : ICrystal
{
    CrystalConfiguration OriginalCrystalConfiguration { get; }

    Waypoint Waypoint { get; }

    ulong LeadingJournalPosition { get; }

    void SetStorage(IStorage storage);
}
