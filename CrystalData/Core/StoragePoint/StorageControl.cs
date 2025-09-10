// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CrystalData.Internal;
using Tinyhand.IO;

namespace CrystalData;

public partial class StorageControl : IPersistable
{
    private const int MinimumDataSize = 256;
    private const int IntervalInMilliseconds = 100;

    /// <summary>
    /// Represents the disabled storage control.
    /// </summary>
    internal static readonly StorageControl Disabled = new();

    #region FiendAndProperty

    /// <summary>
    /// A lock object used for exclusive control for StorageControl, StorageMap, StoragePoint, and StorageObject.<br/>
    /// To prevent deadlocks, do not call external functions while holding this lock.
    /// </summary>
    private readonly Lock lowestLockObject;

    private Crystalizer? crystalizer;
    private StorageMap[] storageMaps;
    private bool isRip;
    private long memoryUsage;
    private StorageObject? onMemoryHead; // head is the most recently used object. head.previous is the least recently used object.

    private StorageObject? saveQueueHead;

    internal ILogger? Logger { get; set; }

    public bool IsRip => this.isRip;

    public long MemoryUsageLimit { get; private set; } = CrystalizerOptions.DefaultMemoryUsageLimit;

    public TimeSpan SaveInterval { get; private set; } = CrystalizerOptions.DefaultSaveInterval;

    /// <summary>
    /// Gets an estimated memory usage.<br/>
    /// Since obtaining the exact size of an object in memory is difficult,<br/>
    /// the returned value should be considered an approximation.
    /// </summary>
    public long MemoryUsage => this.memoryUsage;

    public long AvailableMemory
    {
        get
        {
            var available = this.MemoryUsageLimit - this.MemoryUsage;
            return available > 0 ? available : 0;
        }
    }

    public bool StorageReleaseRequired => this.MemoryUsage > this.MemoryUsageLimit;

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

    Type IPersistable.DataType => typeof(StorageControl);

    #endregion

    public StorageControl()
    {
        this.lowestLockObject = new();
        this.storageMaps = [];
    }

    public void AddStorageMap(StorageMap storageMap)
    {
        using (this.lowestLockObject.EnterScope())
        {
            var length = this.storageMaps.Length;
            Array.Resize(ref this.storageMaps, length + 1);
            this.storageMaps[length] = storageMap;
        }
    }

    internal void Initialize(Crystalizer crystalizer)
    {
        this.crystalizer = crystalizer;
        this.Logger = crystalizer.UnitLogger.GetLogger<StorageControl>();
        this.MemoryUsageLimit = crystalizer.Options.MemoryUsageLimit;
        this.SaveInterval = crystalizer.Options.SaveInterval;
    }

    internal void Rip() => this.isRip = true;

    internal void ResurrectForTesting() => this.isRip = false;

    internal void SetStorageSize(StorageObject node, int newSize)
    {
        newSize = Math.Max(newSize, MinimumDataSize);

        using (this.lowestLockObject.EnterScope())
        {
            if (node.storageMap.IsEnabled)
            {
                this.memoryUsage += newSize - node.size;
            }

            node.size = newSize;
        }
    }

    Task<bool> IPersistable.TestJournal()
        => Task.FromResult(true);

    async Task<CrystalResult> IPersistable.Store(StoreMode storeMode, CancellationToken cancellationToken)
    {
        if (storeMode == StoreMode.StoreOnly)
        {
            await this.StoreObjects(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await this.ReleaseObjects(cancellationToken).ConfigureAwait(false);
        }

        return CrystalResult.Success;
    }

    internal async Task StoreObjects(CancellationToken cancellationToken)
    {
        var list = this.CreateOnMemoryList();
        if (list is null)
        {
            return;
        }

        foreach (var x in list)
        {
            await x.StoreData(StoreMode.StoreOnly).ConfigureAwait(false);
        }
    }

    internal async Task ReleaseObjects(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var list = this.CreateOnMemoryList();
            if (list is null)
            {
                return;
            }

            foreach (var x in list)
            {
                await x.StoreData(StoreMode.TryRelease).ConfigureAwait(false);
            }

            if (this.onMemoryHead is null)
            {// No storage objects to release.
                return;
            }

            try
            {// Since a locked object cannot be released, wait briefly and then attempt to store and release it again.
                await Task.Delay(IntervalInMilliseconds, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    internal async Task ReleaseStorage(CancellationToken cancellationToken)
    {
        while (this.StorageReleaseRequired &&
            !cancellationToken.IsCancellationRequested)
        {
            StorageObject? node;
            using (this.lowestLockObject.EnterScope())
            {
                node = this.onMemoryHead?.onMemoryPrevious; // Get the least recently used node.
                if (node is null)
                {// No storage objects to release.
                    break;
                }

                this.MoveToRecentInternal(node);
            }

            await node.StoreData(StoreMode.TryRelease);
        }
    }

    /// <summary>
    /// Moves the specified <see cref="StorageObject"/> to the most recently used position in the list.
    /// Updates the object's size and the total memory usage if <paramref name="newSize"/> is non-negative.
    /// If the storage map is disabled, only updates its size.
    /// </summary>
    /// <param name="node">The <see cref="StorageObject"/> to move.</param>
    /// <param name="newSize">The new size of the object.</param>
    internal void MoveToRecent(StorageObject node, int newSize)
    {
        newSize = Math.Max(newSize, MinimumDataSize);

        if (node.storageMap.IsEnabled)
        {// If the storage map is enabled, update the size and move to recent.
            using (this.lowestLockObject.EnterScope())
            {
                if (newSize >= 0)
                {
                    this.memoryUsage += newSize - node.size;
                    node.size = newSize;
                }

                this.MoveToRecentInternal(node);
            }
        }
        else
        {// If the storage map is disabled, only update the size.
            if (newSize >= 0)
            {
                using (this.lowestLockObject.EnterScope())
                {
                    node.size = newSize;
                }
            }
        }
    }

    internal void MoveToRecent(StorageObject node)
    {
        if (node.storageMap.IsEnabled)
        {// If the storage map is enabled, move to recent.
            using (this.lowestLockObject.EnterScope())
            {
                this.MoveToRecentInternal(node);
            }
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
            if (!storageMap.IsEnabled)
            {// StorageMap is disabled.
                if (storageObject is null)
                {
                    storageObject = new();
                    storageObject.Initialize(pointId, TinyhandTypeIdentifier.GetTypeIdentifier<TData>(), storageMap);
                    // storageObject.Goshujin = storageMap.StorageObjects;
                }

                return;
            }

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

            storageObject.Initialize(pointId, typeIdentifier, storageMap);
            storageObject.Goshujin = storageMap.StorageObjects;

            if (((IStructualObject)storageMap).TryGetJournalWriter(out var root, out var writer, true) == true)
            {
                writer.Write(JournalRecord.AddItem);
                writer.Write(pointId);
                writer.Write(typeIdentifier);
                root.AddJournalAndDispose(ref writer);
            }
        }
    }

    internal void Release(StorageObject node, bool removeFromStorageMap)
    {
        using (this.lowestLockObject.EnterScope())
        {
            this.ReleaseInternal(node, removeFromStorageMap);
        }
    }

    internal void AddStorage(StorageObject storageObject, ICrystal crystal, StorageId storageId)
    {
        var numberOfHistories = crystal.CrystalConfiguration.StorageConfiguration.NumberOfHistoryFiles;
        ulong fileIdToDelete = default;

        using (this.lowestLockObject.EnterScope())
        {
            if (numberOfHistories <= 1)
            {
                fileIdToDelete = storageObject.storageId0.FileId;
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

    internal void DeleteLatestStorageForTest(StorageObject storageObject)
    {
        ulong fileId = 0;
        using (this.lowestLockObject.EnterScope())
        {
            if (storageObject.storageId1.IsValid)
            {
                fileId = storageObject.storageId0.FileId;
            }
        }

        if (fileId != 0 && storageObject.StructualRoot is ICrystal crystal)
        {
            crystal.Storage.DeleteAsync(ref fileId).Wait();
        }
    }

    internal void EraseStorage(StorageObject storageObject)
    {
        ulong id0;
        ulong id1;
        ulong id2;
        // ulong id3;

        using (this.lowestLockObject.EnterScope())
        {
            this.ReleaseInternal(storageObject, true);

            id0 = storageObject.storageId0.FileId;
            id1 = storageObject.storageId1.FileId;
            id2 = storageObject.storageId2.FileId;
            // id3 = this.storageId3.FileId;

            storageObject.storageId0 = default;
            storageObject.storageId1 = default;
            storageObject.storageId2 = default;
            // this.storageId3 = default;
        }

        if (storageObject.StructualRoot is ICrystal crystal)
        {// Delete storage
            var storage = crystal.Storage;

            if (id0 != 0)
            {
                storage.DeleteAndForget(ref id0);
            }

            if (id1 != 0)
            {
                storage.DeleteAndForget(ref id1);
            }

            if (id2 != 0)
            {
                storage.DeleteAndForget(ref id2);
            }

            /*if (id3 != 0)
            {
                storage.DeleteAndForget(ref id3);
            }*/
        }
    }

    internal async Task<bool> ProcessSaveQueue(StorageObject[] tempArray, Crystalizer crystalizer, CancellationToken cancellationToken)
    {
        var threshold = crystalizer.SystemTimeInSeconds - crystalizer.DefaultSaveDelaySeconds;
        var result = false;
        while (true)
        {
            var count = 0;
            using (this.lowestLockObject.EnterScope())
            {
                while (this.saveQueueHead is not null && count < tempArray.Length)
                {
                    var node = this.saveQueueHead;
                    if (node.saveQueueTime > threshold)
                    {// Not yet time to save.
                        break;
                    }

                    this.saveQueueHead = node.saveQueueNext;
                    if (this.saveQueueHead == node)
                    {// Only one node in the list.
                        this.saveQueueHead = null;
                    }
                    else
                    {
                        node.saveQueueNext!.saveQueuePrevious = node.saveQueuePrevious;
                        node.saveQueuePrevious!.saveQueueNext = node.saveQueueNext;
                    }

                    node.saveQueuePrevious = default;
                    node.saveQueueNext = default;
                    node.saveQueueTime = 0;

                    tempArray[count++] = node;
                }
            }

            if (count == 0)
            {
                break;
            }
            else
            {
                result = true;
            }

            for (var i = 0; i < count; i++)
            {
                await tempArray[i].StoreData(StoreMode.StoreOnly).ConfigureAwait(false);
            }

            Array.Clear(tempArray, 0, count);
            if (count < tempArray.Length)
            {
                break;
            }
        }

        return result;
    }

    internal void AddToSaveQueue(StorageObject node)
    {
        if (this.crystalizer is null)
        {
            return;
        }

        using (this.lowestLockObject.EnterScope())
        {
            if (node.saveQueueTime != 0)
            {
                return;
            }

            node.saveQueueTime = this.crystalizer.SystemTimeInSeconds;

            if (this.saveQueueHead is null)
            {// First node
                this.saveQueueHead = node;
                node.saveQueueNext = node;
                node.saveQueuePrevious = node;
                return;
            }

            node.saveQueueNext = this.saveQueueHead;
            node.saveQueuePrevious = this.saveQueueHead.saveQueuePrevious;
            this.saveQueueHead.saveQueuePrevious!.saveQueueNext = node;
            this.saveQueueHead.saveQueuePrevious = node;
        }
    }

    private void ReleaseInternal(StorageObject node, bool removeFromStorageMap)
    {
        // OnMemory list (Least recently used list).
        if (node.onMemoryPrevious is not null && node.onMemoryNext is not null)
        {
            if (node.storageMap.IsEnabled)
            {
                this.memoryUsage -= node.Size;
            }

            node.size = 0;

            if (node.onMemoryNext == node)
            {
                this.onMemoryHead = null;
            }
            else
            {
                node.onMemoryNext.onMemoryPrevious = node.onMemoryPrevious;
                node.onMemoryPrevious.onMemoryNext = node.onMemoryNext;
                if (this.onMemoryHead == node)
                {
                    this.onMemoryHead = node.onMemoryNext;
                }
            }

            node.onMemoryPrevious = default;
            node.onMemoryNext = default;
        }

        // ToSave list.
        if (node.saveQueuePrevious is not null && node.saveQueueNext is not null)
        {
            node.saveQueueTime = 0;

            if (node.saveQueueNext == node)
            {
                this.saveQueueHead = null;
            }
            else
            {
                node.saveQueueNext.saveQueuePrevious = node.saveQueuePrevious;
                node.saveQueuePrevious.saveQueueNext = node.saveQueueNext;
                if (this.saveQueueHead == node)
                {
                    this.saveQueueHead = node.saveQueueNext;
                }
            }

            node.saveQueuePrevious = default;
            node.saveQueueNext = default;
        }

        if (removeFromStorageMap)
        {
            node.Goshujin = default;
        }
    }

    private void MoveToRecentInternal(StorageObject node)
    {
        if (node.onMemoryNext is null ||
            node.onMemoryPrevious is null)
        {// Not added to the list.
            if (this.onMemoryHead is null)
            {// First node
                this.onMemoryHead = node;
                node.onMemoryNext = node;
                node.onMemoryPrevious = node;
                return;
            }
        }
        else
        {// Remove the node from the list.
            if (node.onMemoryNext == node)
            {// Only one node in the list.
                return;
            }

            node.onMemoryNext.onMemoryPrevious = node.onMemoryPrevious;
            node.onMemoryPrevious.onMemoryNext = node.onMemoryNext;
            if (this.onMemoryHead == node)
            {
                this.onMemoryHead = node.onMemoryNext;
            }
        }

        node.onMemoryNext = this.onMemoryHead;
        node.onMemoryPrevious = this.onMemoryHead!.onMemoryPrevious;
        this.onMemoryHead.onMemoryPrevious!.onMemoryNext = node;
        this.onMemoryHead.onMemoryPrevious = node;
        this.onMemoryHead = node;
    }

    private List<StorageObject>? CreateOnMemoryList()
    {
        List<StorageObject> list = new();
        using (this.lowestLockObject.EnterScope())
        {
            if (this.onMemoryHead is null)
            {// No storage objects to release.
                return null;
            }

            StorageObject node = this.onMemoryHead;
            while (true)
            {
                list.Add(node);
                node = node.onMemoryNext!;
                if (node == this.onMemoryHead)
                {// Reached back to the head.
                    break;
                }
            }
        }

        return list;
    }
}
