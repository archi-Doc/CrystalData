// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

using System.Diagnostics.CodeAnalysis;
using CrystalData.Internal;

namespace CrystalData;

[TinyhandObject]
public sealed partial class StorageMap
{
    public const string Filename = "Map";

    #region FiendAndProperty

    private readonly bool enabledStorage;

    [Key(0)]
    private StorageObject.GoshujinClass storagePoints = new();

    private long storageUsage;

    public bool IsEnabled => this.enabledStorage;

    public bool IsDisabled => !this.enabledStorage;

    #endregion

    public StorageMap(bool enabled = true)
    {
        this.enabledStorage = enabled;
    }

    public void Initialize()
    {
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

    public void GetOrCreate<TData>(ref ulong pointId, [NotNull] ref StorageObject? storageObject)
    {
        if (this.IsInvalid)
        {
            using (InvalidLockObject.EnterScope())
            {
                if (storageObject is not null)
                {
                    return;
                }

                var typeIdentifier = TinyhandTypeIdentifier.GetTypeIdentifier<TData>();
                storageObject = new StorageObject(typeIdentifier);
                pointId = 0;
                return;
            }
        }

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

            var typeIdentifier2 = TinyhandTypeIdentifier.GetTypeIdentifier<TData>();
            storageObject = new StorageObject(id, typeIdentifier2);
            storageObject.Goshujin = this.storagePoints;
            pointId = id;
        }
    }

    private void UpdateStorageUsageInternal(long size)
        => this.storageUsage += size;
}
