// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Represents the state of a <see cref="StoragePointObsolete{TData}"/>.
/// </summary>
public enum StoragePointState
{
    /// <summary>
    /// The function of <see cref="StoragePointObsolete{TData}" /> is invalid, and the process is bypassed as is.
    /// </summary>
    InvalidStorage,

    /// <summary>
    /// The data is stored on storage and is not loaded into memory.
    /// </summary>
    OnStorage,

    /// <summary>
    /// The data is loaded in memory.
    /// </summary>
    InMemory,

    /// <summary>
    /// The data is loaded in memory and exclusively locked.
    /// </summary>
    Locked,

    /// <summary>
    /// The unloading process is in progress, and operations on the data are disabled.
    /// </summary>
    UnloadingInProgress,

    /// <summary>
    /// The data is unloaded, and operations on the data are disabled.
    /// </summary>
    Unloaded,
}
