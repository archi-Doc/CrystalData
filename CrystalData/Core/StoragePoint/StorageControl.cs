// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

using CrystalData.Internal;

namespace CrystalData;

public partial class StorageControl
{
    #region FiendAndProperty

    private readonly Lock lockObject = new();
    private long storageUsage;
    private long memoryUsage;

    public long StorageUsage => this.memoryUsage;

    public long MemoryUsage => this.memoryUsage;

    #endregion

    internal StorageControl()
    {
    }

    public void UpdateStorageUsage(long size)
        => Interlocked.Add(ref this.storageUsage, size);

    public void UpdateMemoryUsage(long size)
        => Interlocked.Add(ref this.memoryUsage, size);

    public bool TryRemove(StorageObject storageObject)
    {
        using (this.lockObject.EnterScope())
        {
            if (storageObject.storageControl != this)
            {
                return false;
            }

            storageObject.Goshujin = default;
            this.UpdateStorageUsageInternal(-storageObject.Size);
            this.UpdateMemoryUsageInternal(-storageObject.Size);//
        }

        return true;
    }

    private void UpdateStorageUsageInternal(long size)
    {
        this.storageUsage += size;
    }

    private void UpdateMemoryUsageInternal(long size)
    {
        this.memoryUsage += size;
    }
}
