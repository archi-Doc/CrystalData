// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public enum SaveFormat
{
    /// <summary>
    /// The save format is specified in <see cref="CrystalOptions"/>.<br/>
    /// If not specified, data is saved in binary format.
    /// </summary>
    Default,

    /// <summary>
    /// Data is saved in binary format.
    /// </summary>
    Binary,

    /// <summary>
    /// Data is saved in utf-8 format.
    /// </summary>
    Utf8,
}
