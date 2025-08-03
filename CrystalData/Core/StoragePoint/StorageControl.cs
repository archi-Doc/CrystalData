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
    /// A lock object used for exclusive control for StorageControl, StorageMap, StoragePoint, and StorageObject.<br/>
    /// To prevent deadlocks, do not call external functions while holding this lock.
    /// </summary>
    private readonly Lock lowestLockObject;

    private StorageMap[] storageMaps;
    private long memoryUsage;
    private StorageObject? head; // head is the most recently used object. head.previous is the least recently used object.

    /// <summary>
    /// Gets <see cref="StorageMap" /> for <see cref="StorageObject" /> with storage disabled.
    /// </summary>
    public StorageMap DisabledMap { get; }

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
        if (storageObject.storageMap.IsDisabled)
        {
            return;
        }

        using (this.lowestLockObject.EnterScope())
        {
            if (this.head == null)
            {
                storageObject.next = storageObject;
                storageObject.previous = storageObject;
                this.head = storageObject;
            }
            else
            {
                storageObject.next = this.head;
                storageObject.previous = this.head.previous;
                this.head.previous!.next = storageObject;
                this.head.previous = storageObject;
                this.head = storageObject;
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

            // Least recently used list.
            if (storageObject.next == storageObject)
            {
                this.head = null;
            }
            else
            {
                storageObject.next!.previous = storageObject.previous;
                storageObject.previous!.next = storageObject.next;
                if (this.head == storageObject)
                {
                    this.head = storageObject.next;
                }
            }
        }

        return true;
    }

    private void UpdateMemoryUsageInternal(long size)
        => this.memoryUsage += size;
}
