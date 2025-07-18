// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public enum StoreDataMode
{
    /// <summary>
    /// The data is persisted. The data in memory remains unchanged.
    /// </summary>
    StoreOnly,

    /// <summary>
    /// Attempts to persist the data and release resources. If the data is locked, neither persistence nor release will be performed.
    /// </summary>
    TryRelease,

    /// <summary>
    /// The data is persisted and forcibly unloaded.
    /// </summary>
    ForceRelease,
}

public interface IStoragePoint
{
    Task<bool> StoreData(StoreDataMode unloadMode);

    Type DataType { get; }
}
