// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Represents the state of a <see cref="StoragePoint{TData}"/>.
/// </summary>
public enum StoragePointState
{
    /// <summary>
    /// The function of <see cref="StoragePoint{TData}" /> is disabled, and the process is bypassed as is.
    /// </summary>
    Disabled,

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
    /// The data has been unloaded and is no longer available.<br/>
    /// This is intended for data persistence at application shutdown.
    /// </summary>
    Rip,
}
