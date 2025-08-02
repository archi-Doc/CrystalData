// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

using CrystalData.Internal;

namespace CrystalData;

public partial class StorageControl
{
    public static readonly StorageControl Default = new();

    #region FiendAndProperty

    /// <summary>
    /// This is an object used for exclusive control for StorageControl, StorageMap, StoragePoint, and StorageObject.<br/>
    /// To prevent deadlocks, do not call external functions while holding the lock.
    /// </summary>
    private readonly Lock lowestLockObject = new();
    // private long storageUsage;
    private long memoryUsage;

    /// <summary>
    /// Gets <see cref="StorageMap" /> for <see cref="StorageObject" /> with storage disabled.
    /// </summary>
    public StorageMap DisabledMap { get; } = new();

    // public long StorageUsage => this.storageUsage;

    public long MemoryUsage => this.memoryUsage;

    #endregion

    private StorageControl()
    {
    }

    public bool TryRemove(StorageObject storageObject)
    {
        using (this.lowestLockObject.EnterScope())
        {
            storageObject.Goshujin = default;
            if (storageObject.storageMap.IsEnabled)
            {
                this.UpdateMemoryUsageInternal(-storageObject.Size);
            }
        }

        return true;
    }

    private void UpdateMemoryUsageInternal(long size)
        => this.memoryUsage += size;
}
