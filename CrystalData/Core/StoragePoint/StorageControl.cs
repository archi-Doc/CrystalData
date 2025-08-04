// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    private bool isRip;
    private long memoryUsage;
    private StorageObject? head; // head is the most recently used object. head.previous is the least recently used object.

    /// <summary>
    /// Gets <see cref="StorageMap" /> for <see cref="StorageObject" /> with storage disabled.
    /// </summary>
    public StorageMap DisabledMap { get; }

    public bool IsRip => this.isRip;

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

    internal void MoveToRecent(StorageObject node, long sizeDifference)
    {
        if (node.storageMap.IsDisabled)
        {
            return;
        }

        using (this.lowestLockObject.EnterScope())
        {
            this.memoryUsage += sizeDifference;

            if (node.next is null ||
                node.previous is null)
            {// Not added to the list.
                if (this.head is null)
                {// First node
                    this.head = node;
                    node.next = node;
                    node.previous = node;
                    return;
                }
            }
            else
            {// Remove the node from the list.
                if (node.next == node)
                {// Only one node in the list.
                    return;
                }

                node.next.previous = node.previous;
                node.previous.next = node.next;
                if (this.head == node)
                {
                    this.head = node.next;
                }
            }

            node.next = this.head;
            node.previous = this.head!.previous;
            this.head.previous!.next = node;
            this.head.previous = node;
            this.head = node;
        }
    }

    /// <summary>
    /// This is a relatively complex function and forms the core of <see cref="StorageObject"/> creation.<br/>
    /// For a new <see cref="StorageObject"/>, it is created and added to the <see cref="StorageMap"/>.<br/>
    /// If the <see cref="StorageObject"/> is already associated with a <see cref="StorageMap"/>, it is first removed and then added to the new <see cref="StorageMap"/>.
    /// </summary>
    /// <typeparam name="TData">The type of data to be stored in the <see cref="StorageObject"/>.</typeparam>
    /// <param name="pointId">The identifier of the storage point.</param>
    /// <param name="storageObject">A reference to the <see cref="StorageObject"/> instance. If null, a new instance will be created.</param>
    /// <param name="storageMap">The <see cref="StorageMap"/> to which the <see cref="StorageObject"/> will be associated.</param>
    internal void GetOrCreate<TData>(ref ulong pointId, [NotNull] ref StorageObject? storageObject, StorageMap storageMap)
    {
        using (this.lowestLockObject.EnterScope())
        {
            uint typeIdentifier;
            if (storageObject is null)
            {// Create a new object.
                if (pointId != 0 &&
                    storageMap.StorageObjects.PointIdChain.TryGetValue(pointId, out storageObject!))
                {// Found existing StorageObject.
                    return;
                }

                typeIdentifier = TinyhandTypeIdentifier.GetTypeIdentifier<TData>();
                pointId = RandomVault.Default.NextUInt64();
                storageObject = new();
            }
            else
            {// Use an existing object.
                if (storageObject.Goshujin == storageMap.StorageObjects)
                {// Already exists in the specified StorageMap.
                    return;
                }

                typeIdentifier = storageObject.TypeIdentifier;
                pointId = storageObject.PointId;
                storageObject.Goshujin = default;
            }

            while (true)
            {
                if (!storageMap.StorageObjects.PointIdChain.ContainsKey(pointId))
                {
                    break;
                }

                pointId = RandomVault.Default.NextUInt64();
            }

            storageObject.Initialize(pointId, typeIdentifier, storageMap.IsDisabled);
            storageObject.Goshujin = storageMap.StorageObjects;
        }
    }

    internal void Release(StorageObject node, bool removeFromStorageMap)
    {
        using (this.lowestLockObject.EnterScope())
        {
            // Least recently used list.
            if (node.previous is not null &&
                node.next is not null)
            {
                if (node.storageMap.IsEnabled)
                {
                    this.UpdateMemoryUsageInternal(-node.Size);
                }

                if (node.next == node)
                {
                    this.head = null;
                }
                else
                {
                    node.next.previous = node.previous;
                    node.previous.next = node.next;
                    if (this.head == node)
                    {
                        this.head = node.next;
                    }
                }

                node.previous = default;
                node.next = default;
            }

            if (removeFromStorageMap)
            {
                node.Goshujin = default;
            }
        }
    }

    internal void AddStorage(StorageObject storageObject, ICrystal crystal, StorageId storageId)
    {
        var numberOfHistories = crystal.CrystalConfiguration.NumberOfFileHistories;
        ulong fileIdToDelete = default;

        using (this.lowestLockObject.EnterScope())
        {
            if (numberOfHistories <= 1)
            {
                storageObject.storageId0 = storageId;
            }
            else if (numberOfHistories == 2)
            {
                fileIdToDelete = storageObject.storageId1.FileId;
                storageObject.storageId1 = storageObject.storageId0;
                storageObject.storageId0 = storageId;
            }
            else
            {
                fileIdToDelete = storageObject.storageId2.FileId;
                storageObject.storageId2 = storageObject.storageId1;
                storageObject.storageId1 = storageObject.storageId0;
                storageObject.storageId0 = storageId;
            }
        }

        if (fileIdToDelete != 0)
        {// Delete the oldest file.
            crystal.Storage.DeleteAndForget(ref fileIdToDelete);
        }
    }

    private void UpdateMemoryUsageInternal(long size)
        => this.memoryUsage += size;
}
