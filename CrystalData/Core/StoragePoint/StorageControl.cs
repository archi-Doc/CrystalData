// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

using CrystalData.Internal;

namespace CrystalData;

public partial class StorageControl
{
    #region FiendAndProperty

    private readonly StorageObject.GoshujinClass storagePoints = new();
    private long memoryUsage;

    public long MemoryUsage => this.memoryUsage;

    #endregion

    internal StorageControl(Crystalizer crystalizer)
    {
    }

    public void Update(ulong pointId)
    {
        if (pointId == 0)
        {
            return;
        }

        using (this.storagePoints.LockObject.EnterScope())
        {
            if (this.storagePoints.PointIdChain.TryGetValue(pointId, out var obj))
            {// Found
                this.storagePoints.LastAccessedChain.AddFirst(obj);
            }
        }
    }

    public void UpdateMemoryUsage(int size)
    {
        Interlocked.Add(ref this.memoryUsage, size);
    }

    public StorageObject GetOrCreate(ref ulong pointId, uint typeIdentifier)
    {
        using (this.storagePoints.LockObject.EnterScope())
        {
            if (pointId != 0 &&
                this.storagePoints.PointIdChain.TryGetValue(pointId, out var obj))
            {// Found existing StoragePoint.
                return obj;
            }

            while (true)
            {
                pointId = RandomVault.Default.NextUInt64();
                if (!this.storagePoints.PointIdChain.ContainsKey(pointId))
                {
                    break;
                }
            }

            obj = new StorageObject(pointId, typeIdentifier);
            obj.Goshujin = this.storagePoints;
            return obj;
        }
    }
}
