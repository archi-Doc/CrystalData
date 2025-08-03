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

    public StorageControl StorageControl { get; }

    private bool enabledStorageMap = true;

    [Key(0)]
    private StorageObject.GoshujinClass storageObjects = new();

    private long storageUsage;

    public bool IsEnabled => this.enabledStorageMap;

    public bool IsDisabled => !this.enabledStorageMap;

    public long StorageUsage => this.storageUsage;

    #endregion

    public StorageMap(StorageControl storageControl)
    {
        this.StorageControl = storageControl;
        storageControl.AddStorageMap(this);
    }

    public void GetOrCreate<TData>(ref ulong pointId, [NotNull] ref StorageObject? storageObject, StorageMap storageMap)
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

        using (this.storageObjects.LockObject.EnterScope())
        {
            if (storageObject is not null)
            {
                return;
            }

            var id = pointId;
            if (id != 0 &&
                this.storageObjects.PointIdChain.TryGetValue(id, out storageObject!))
            {// Found existing StoragePoint.
                return;
            }

            while (true)
            {
                id = RandomVault.Default.NextUInt64();
                if (!this.storageObjects.PointIdChain.ContainsKey(id))
                {
                    break;
                }
            }

            var typeIdentifier2 = TinyhandTypeIdentifier.GetTypeIdentifier<TData>();
            storageObject = new StorageObject(id, typeIdentifier2);
            storageObject.Goshujin = this.storageObjects;
            pointId = id;
        }
    }

    internal void DisableStorageMap()
    {
        this.enabledStorageMap = false;
    }

    private void UpdateStorageUsageInternal(long size)
        => this.storageUsage += size;
}
