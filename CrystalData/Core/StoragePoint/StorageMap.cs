// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

using System.Diagnostics.CodeAnalysis;
using CrystalData.Internal;

namespace CrystalData;

[TinyhandObject]
public sealed partial class StorageMap
{
    public const string Filename = "Map";

    public static readonly StorageMap Invalid = new(true);

    #region FiendAndProperty

    private readonly bool invalidStorageControl;

    [Key(0)]
    private StorageObject.GoshujinClass storagePoints = new();
    private long memoryUsage;

    public bool IsValid => !this.invalidStorageControl;

    public bool IsInvalid => this.invalidStorageControl;

    public long MemoryUsage => this.memoryUsage;

    #endregion

    internal StorageMap(bool invalid)
    {
        this.invalidStorageControl = invalid;
    }

    public void Update(ulong pointId)
    {
        if (this.IsInvalid)
        {
            return;
        }

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
        if (this.IsInvalid)
        {
            return;
        }

        Interlocked.Add(ref this.memoryUsage, size);
    }

    public bool TryRemove(StorageObject storageObject)
    {
        if (this.IsInvalid)
        {
            return true;
        }

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

    public void GetOrCreate<TData>(ref ulong pointId, [NotNull] ref StorageObject? storageObject)
    {
        using (this.storagePoints.LockObject.EnterScope())
        {
            if (storageObject is not null)
            {
                return;
            }

            if (this.IsInvalid)
            {
                var typeIdentifier = TinyhandTypeIdentifier.GetTypeIdentifier<TData>();
                storageObject = new StorageObject(typeIdentifier);
                pointId = 0;
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

            var typeIdentifier2 = TinyhandTypeIdentifier.GetTypeIdentifier<TData>();
            storageObject = new StorageObject(id, typeIdentifier2);
            storageObject.Goshujin = this.storagePoints;
            pointId = id;
        }
    }
}
