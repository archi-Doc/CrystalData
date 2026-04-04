// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Internal;

/// <summary>
/// Represents a set of state flags associated with a storage object.
/// </summary>
[Flags]
internal enum StorageObjectState : byte
{
    /// <summary>
    /// Indicates the object is pinned and guaranteed to remain only in memory<br/> and will never be released and written to disk.
    /// </summary>
    Pinned = 1,

    /// <summary>
    /// The data has been invalidated and can no longer be used.<br/>
    /// Use this before deleting the parent or for other similar purposes.
    /// </summary>
    Invalidated = 2,
}
