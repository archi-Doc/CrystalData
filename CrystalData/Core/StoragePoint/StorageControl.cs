// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

using CrystalData.Internal;

namespace CrystalData;

public partial class StorageControl
{
    /// <summary>
    /// This is the default instance of StorageControl.<br/>
    /// I know it’s not ideal to use it as a static, but...
    /// </summary>
    public static readonly StorageControl Default = new();

    #region FiendAndProperty

    /// <summary>
    /// This is an object used for exclusive control for StorageControl, StorageMap, StoragePoint, and StorageObject.<br/>
    /// To prevent deadlocks, do not call external functions while holding the lock.
    /// </summary>
    private readonly Lock lowestLockObject;
    private StorageMap[] storageMaps;
    // private long storageUsage;
    private long memoryUsage;

    /// <summary>
    /// Gets <see cref="StorageMap" /> for <see cref="StorageObject" /> with storage disabled.
    /// </summary>
    public StorageMap DisabledMap { get; }

    // public long StorageUsage => this.storageUsage;

    public long MemoryUsage => this.memoryUsage;

    public long StorageUsage
    {
        get
        {
            var maps = this.storageMaps;
            long usage = 0;
            foreach (var x in maps)
            {
                usage += x.StorageUsage;
            }

            return usage;
        }
    }

    #endregion

    public StorageControl()
    {
        this.lowestLockObject = new();
        this.storageMaps = [];

        this.DisabledMap = new(this);
        this.DisabledMap.DisableStorageMap();
    }

    public void AddStorageMap(StorageMap storageMap)
    {
        using (this.lowestLockObject.EnterScope())
        {
            var length = this.storageMaps.Length;
            Array.Resize(ref this.storageMaps, length);
            this.storageMaps[length] = storageMap;
        }
    }

    public void MoveToRecent(StorageObject storageObject)
    {
        using (this.lowestLockObject.EnterScope())
        {
            if (storageObject.storageMap.IsEnabled)
            {
                storageObject.storageMap.Update(storageObject.PointId);
            }
        }
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
