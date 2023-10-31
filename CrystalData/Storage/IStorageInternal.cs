// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

internal interface IStorageInternal
{
    Task<bool> TestJournal();
}
