// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

using System.Diagnostics.CodeAnalysis;
using CrystalData.Internal;

namespace CrystalData;

public partial class StorageControl
{
    public static readonly StorageControl Default = new();

    #region FiendAndProperty

    private readonly StorageObject.GoshujinClass storagePoints = new();
    private long memoryUsage;

    public long MemoryUsage => this.memoryUsage;

    #endregion

    internal StorageControl()
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

    public bool TryRemove(StorageObject storageObject)
    {
        using (this.storagePoints.LockObject.EnterScope())
        {
            if (storageObject.Goshujin != this.storagePoints)
            {
                return false;
            }

            storageObject.Goshujin = default;
            this.UpdateMemoryUsage(-storageObject.Size);
        }

        return true;
    }

    public bool TryAdd(StorageObject storageObject)
    {
        using (this.storagePoints.LockObject.EnterScope())
        {
            var goshujin = storageObject.Goshujin;
            if (goshujin == this.storagePoints)
            {// Already added.
                return true;
            }
            else if (goshujin is not null)
            {
                return false;
            }

            storageObject.Goshujin = this.storagePoints;
        }

        return true;
    }

    public void GetOrCreate(ref ulong pointId, uint typeIdentifier, [NotNull] ref StorageObject? storageObject)
    {
        using (this.storagePoints.LockObject.EnterScope())
        {
            if (storageObject is not null)
            {
                return;
            }

            var id = pointId;
            if (id != 0 &&
                this.storagePoints.PointIdChain.TryGetValue(id, out storageObject!))
            {// Found existing StoragePoint.
                return;
            }

            while (true)
            {
                id = RandomVault.Default.NextUInt64();
                if (!this.storagePoints.PointIdChain.ContainsKey(id))
                {
                    break;
                }
            }

            storageObject = new StorageObject(id, typeIdentifier);
            storageObject.Goshujin = this.storagePoints;
            pointId = id;
        }
    }
}
