// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public enum SavePolicy
{
    /// <summary>
    /// The saving policy is specified in <see cref="CrystalizerOptions"/>.<br/>
    /// If not specified, <see cref="SavePolicy.Manual"/> is used as default.
    /// </summary>
    Default,

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
}
