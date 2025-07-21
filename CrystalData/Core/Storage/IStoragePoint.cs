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

public enum ProbeMode
{
    /// <summary>
    /// For the unload process, attempts are made to lock all child elements.<br/>
    /// If locking is successful, it returns true; if not, it returns false without changing the state.
    /// </summary>
    TryLockAll,


    IsUnloadedAll,
}

/// <summary>
/// <see cref="IStoragePoint"/> is a inteface of <see cref="StoragePoint{TData}" /> responsible for loading and persisting partial data.
/// </summary>
public interface IStoragePoint
{
    /// <summary>
    /// Saves the data associated with the storage point using the specified unload mode.
    /// </summary>
    /// <param name="unloadMode">
    /// Specifies the unload behavior for persisting and releasing resources.
    /// </param>
    /// <returns>
    /// A <see cref="Task{Boolean}"/> representing the asynchronous save operation. Returns <c>true</c> if the save was successful; otherwise, <c>false</c>.
    /// </returns>
    Task<bool> Save(UnloadMode2 unloadMode);

    bool Probe(ProbeMode probeMode);

    /// <summary>
    /// Gets the <see cref="Type"/> of the data managed by this storage point.
    /// </summary>
    Type DataType { get; }
}
