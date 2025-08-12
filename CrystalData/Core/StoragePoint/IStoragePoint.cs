// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public enum ProbeMode
{
    /// <summary>
    /// Checks whether all child elements can be unloaded.<br/>
    /// Returns true if all are unloadable (i.e., not locked); otherwise, returns false.
    /// </summary>
    IsUnloadableAll,

    /// <summary>
    /// Checks whether all child elements are unloaded.<br/>
    /// Returns true if all are unloaded (or InvalidStorage); otherwise, returns false.
    /// </summary>
    IsUnloadedAll,

    /// <summary>
    /// Lock all child elements.
    /// </summary>
    LockAll,

    /// <summary>
    /// Unlock all child elements.
    /// </summary>
    UnlockAll,

    Remove,
}

/// <summary>
/// <see cref="IStoragePoint"/> is a inteface of <see cref="StoragePoint{TData}" /> responsible for loading and persisting partial data.
/// </summary>
public interface IStoragePoint
{
    /// <summary>
    /// Saves the data associated with the storage point using the specified unload mode.
    /// </summary>
    /// <param name="storeMode">
    /// Specifies the unload behavior for persisting and releasing resources.
    /// </param>
    /// <returns>
    /// A <see cref="Task{Boolean}"/> representing the asynchronous save operation. Returns <c>true</c> if the save was successful; otherwise, <c>false</c>.
    /// </returns>
    Task<bool> StoreData(StoreMode storeMode);

    /*
    /// <summary>
    /// Probes the storage point and its child elements using the specified probe mode.
    /// </summary>
    /// <param name="probeMode">
    /// The <see cref="ProbeMode"/> that determines the type of probe operation to perform, such as checking unloadability, lock state, or unloaded state.
    /// </param>
    /// <returns>
    /// <c>true</c> if the probe operation succeeds according to the specified mode; otherwise, <c>false</c>.
    /// </returns>
    bool Probe(ProbeMode probeMode);

    /// <summary>
    /// Gets the <see cref="Type"/> of the data managed by this storage point.
    /// </summary>
    Type DataType { get; }*/
}
