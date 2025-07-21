// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public enum UnloadMode2
{
    /// <summary>
    /// The data is persisted, but the memory state is not changed.
    /// </summary>
    NoUnload,

    /// <summary>
    /// Attempts to persist the data and release resources. If the data is locked, neither persistence nor release will be performed.
    /// </summary>
    TryUnload,

    /// <summary>
    /// The data is persisted, and the process waits until unloading is complete.
    /// </summary>
    AlwaysUnload,

    /// <summary>
    /// Similar to <see cref="AlwaysUnload" />, this waits until the unload process is complete.<br/>
    /// Additionally, it sets the target StoragePoint to the unloaded state.<br/>
    /// This is intended for use during service or application shutdown.
    /// </summary>
    All,
}

/// <summary>
/// <see cref="IStoragePoint"/> is a inteface of <see cref="StoragePoint{TData}" /> responsible for loading and persisting partial data.
/// </summary>
public interface IStoragePoint
{
    Task<bool> Save(UnloadMode2 unloadMode);

    Type DataType { get; }
}
