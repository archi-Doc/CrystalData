// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

namespace CrystalData;

public partial class StorageControl
{
    #region FiendAndProperty

    private long storageUsage;
    private long memoryUsage;

    public long StorageUsage => this.memoryUsage;

    public long MemoryUsage => this.memoryUsage;

    #endregion

    internal StorageControl()
    {
    }

    public void UpdateStorageUsage(long size)
    {
        Interlocked.Add(ref this.storageUsage, size);
    }

    public void UpdateMemoryUsage(long size)
    {
        Interlocked.Add(ref this.memoryUsage, size);
    }
}
