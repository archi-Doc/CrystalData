// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public enum SavePolicy
{
    /// <summary>
    /// Timing of saving data is controlled by the application [default].
    /// </summary>
    Manual,

    /// <summary>
    /// Data is volatile and not saved.
    /// </summary>
    Volatile,

    /// <summary>
    /// Data will be saved at regular intervals.
    /// </summary>
    Periodic,

    /// <summary>
    /// When the data is changed, it is registered in the save queue and will be saved in a second.
    /// </summary>
    OnChanged,
}
