// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

internal interface IStorageInternal
{
    Task PersistStorage(ICrystal? callingCrystal);

    Task<bool> TestJournal();
}
