// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Threading;
using CrystalData.Journal;
using Tinyhand.IO;

namespace CrystalData.Internal;

#pragma warning disable SA1202 // Elements should be ordered by access
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1401 // Fields should be private

[TinyhandObject(ExplicitKeysOnly = true)]
[ValueLinkObject]
public sealed partial class StorageObject : SemaphoreLock, IStructuralObject, IStructuralRoot, IDataUnlocker, IEquatable<StorageObject>
{// object:(16), protectionState:4, pointId:8, typeIdentifier:4, storageId:24x3, storageMap:8, onMemoryPrevious:8, onMemoryNext:8, saveQueueTime:4, saveQueuePrevious:8, saveQueueNext:8, data:8, size:4, Goshujin:8, Link:4+4, SemaphoreLock:39
    public const int MaxHistories = 3;

    #region FieldAndProperty

    internal StorageObjectState storageObjectState; // Lock:StorageControl
    internal byte protectionState;

    [Key(0)]
    [Link(Primary = true, Unique = true, Type = ChainType.Unordered, AddValue = false)]
    private ulong pointId; // Lock:StorageControl

    [Key(1)]
    private uint typeIdentifier; // Lock:StorageControl

    [Key(2)]
    internal StorageId storageId0; // Lock:StorageControl

    [Key(3)]
    internal StorageId storageId1; // Lock:StorageControl

    [Key(4)]
    internal StorageId storageId2; // Lock:StorageControl

    internal StorageMap storageMap; // Lock:StorageControl

    internal StorageObject? onMemoryPrevious; // Lock:StorageControl
    internal StorageObject? onMemoryNext; // Lock:StorageControl

    internal int saveQueueTime; // Lock:StorageControl System time in seconds registered in the save queue
    internal StorageObject? saveQueuePrevious; // Lock:StorageControl
    internal StorageObject? saveQueueNext; // Lock:StorageControl

    private object? data; // Lock:this
    internal int size; // Lock:StorageControl

    public IStructuralRoot? StructuralRoot
    {
        get => this;
        set { }
    }

    public IStructuralObject? StructuralParent
    {
        get => default;
        set { }
    }

    public int StructuralKey
    {
        get => -1;
        set { }
    }

    public ulong PointId => this.pointId;

    public uint TypeIdentifier => this.typeIdentifier;

    public int Size => this.size;

    internal StorageControl storageControl => this.storageMap.StorageControl;

    public bool IsEnabled => this.storageMap.IsEnabled;

    public bool IsPinned => this.storageObjectState.HasFlag(StorageObjectState.Pinned);

    public bool IsDeleted => ObjectProtectionStateHelper.IsObsolete(this.protectionState);

    #endregion

    internal StorageObject()
    {
        this.storageMap = StorageMap.Disabled;
    }

    internal void Initialize(ulong pointId, uint typeIdentifier, StorageMap storageMap)
    {// Lock:StorageControl
        this.pointId = pointId;
        this.typeIdentifier = typeIdentifier;
        this.storageMap = storageMap;
    }

    internal async ValueTask<TData> PinData<TData>()
        where TData : class
    {
        using (this.EnterScope())
        {
            if (this.data is null)
            {// PrepareAndLoad
                await this.PrepareAndLoadInternal<TData>().ConfigureAwait(false);
            }

            if (this.data is null)
            {
                this.SetDataInternal(TinyhandSerializer.Reconstruct<TData>(), false, default);
            }
        }

        this.storageControl.PinObject(this);

        return (TData)this.data;
    }

    internal void SerializeStoragePoint(ref TinyhandWriter writer, TinyhandSerializerOptions options)
    {
        if (options.IsSignatureMode)
        {// Signature
            writer.Write(0x8bc0a639u);
            writer.Write(this.pointId);
            return;
        }

        if (!this.IsEnabled && this.data is not null)
        {// Storage disabled
            TinyhandTypeIdentifier.TrySerializeWriter(ref writer, this.typeIdentifier, this.data, options);
        }
        else
        {// In-class
            writer.Write(this.pointId);
        }
    }

    internal async ValueTask<TData?> TryGet<TData>(TimeSpan timeout, CancellationToken cancellationToken)
        where TData : class
    {
        if (this.data is { } data)
        {
            if (this.IsDeleted)
            {// Deleted
                return default;
            }

            this.storageControl.UpdateLink(this);
            return (TData)data;
        }

        if (!await this.EnterAsync(timeout, cancellationToken).ConfigureAwait(false))
        {// Timeout or cancellation
            return default;
        }

        try
        {
            if (this.IsDeleted)
            {// Deleted
                return default;
            }

            if (this.data is null)
            {// PrepareAndLoad
                await this.PrepareAndLoadInternal<TData>().ConfigureAwait(false);
            }

            return this.data is null ? default : (TData)this.data;
        }
        finally
        {
            this.Exit();
        }
    }

    internal async ValueTask<DataScope<TData>> TryLock<TData>(IStructuralObject storagePoint, AcquisitionMode acquisitionMode, TimeSpan timeout, CancellationToken cancellationToken)
        where TData : class
    {
        if (this.IsDeleted)
        {// Deleted
            return new(DataScopeResult.Obsolete);
        }

        if (this.storageControl.IsRip)
        {// Rip
            return new(DataScopeResult.Rip);
        }

        if (!await this.EnterAsync(timeout, cancellationToken).ConfigureAwait(false))
        {// Timeout or cancellation
            return new(DataScopeResult.Timeout);
        }

        if (this.storageControl.IsRip)
        {
            this.Exit();
            return new(DataScopeResult.Rip);
        }

        // Unprotected -> Protected
        if (!ObjectProtectionStateHelper.TryProtect(ref this.protectionState))
        {// Protected(?) or Deleted
            this.Exit();
            return new(DataScopeResult.Obsolete);
        }

        if (this.data is null)
        {// PrepareAndLoad
            await this.PrepareAndLoadInternal<TData>().ConfigureAwait(false);
        }
        else
        {// Already loaded
            this.storageControl.UpdateLink(this);
        }

        if (this.data is not null)
        {// Data loaded
            if (acquisitionMode == AcquisitionMode.Create)
            {
                this.Exit();
                return new(DataScopeResult.AlreadyExists);
            }

            // Get or GetOrCreate
        }
        else
        {// Data not loaded
            if (acquisitionMode == AcquisitionMode.Get)
            {// Get only
                this.Exit();
                return new(DataScopeResult.NotFound);
            }
            else
            {// Create or GetOrCreate -> Reconstruct
                this.SetDataInternal(TinyhandSerializer.Reconstruct<TData>(), false, default);
            }
        }

        return new((TData)this.data, this, storagePoint);
    }

    public void Unlock()
    {// Lock:this
        // Protected -> Unprotected
        ObjectProtectionStateHelper.TryUnprotect(ref this.protectionState);

        this.Exit();
    }

    public bool UnlockAndDelete()
    {// Lock:this
        // -> Deleted
        var deleted = ObjectProtectionStateHelper.TryMarkPendingDeletion(ref this.protectionState);
        this.Exit();

        return deleted;
    }

    public override string ToString()
        => $"PointId={this.pointId}, TypeIdentifier={this.typeIdentifier}, {this.storageId0}, {this.storageId1}, {this.storageId2}";

    public bool Equals(StorageObject? other)
    {
        if (other is null)
        {
            return false;
        }

        return this.pointId == other.pointId &&
            this.typeIdentifier == other.typeIdentifier &&
            this.storageId0.Equals(ref other.storageId0) &&
            this.storageId1.Equals(ref other.storageId1) &&
            this.storageId2.Equals(ref other.storageId2);
    }

    public override int GetHashCode()
        => (int)this.pointId;

    internal void DeleteLatestStorageForTest()
    {
        this.storageControl.DeleteLatestStorageForTest(this);
    }

    internal void Set<TData>(TData data)
        where TData : class
    {
        using (this.EnterScope())
        {
            if (!ObjectProtectionStateHelper.TryProtect(ref this.protectionState))
            {// Protected or Deleted
                return;
            }

            this.SetDataInternal(data, true, default);

            ObjectProtectionStateHelper.TryUnprotect(ref this.protectionState);
        }
    }

    internal async Task<bool> TestJournal(SimpleJournal journal)
    {
        var storage = this.storageMap.Storage;
        object? previousData = default;

        for (var i = MaxHistories - 1; i >= 0; i--)
        {
            var storageId = i switch
            {
                0 => this.storageId0,
                1 => this.storageId1,
                2 => this.storageId2,
                _ => throw new InvalidOperationException(),
            };

            if (!storageId.IsValid)
            {
                continue;
            }

            var fileId = storageId.FileId;
            var result = await storage.GetAsync(ref fileId).ConfigureAwait(false);
            object? currentData;
            try
            {
                if (result.IsFailure ||
                FarmHash.Hash64(result.Data.Span) != storageId.Hash)
                {
                    return false;
                }

                currentData = TinyhandTypeIdentifier.TryDeserialize(this.TypeIdentifier, result.Data.Span);
                if (currentData is null)
                {
                    return false;
                }

                if (previousData is not null)
                {// Compare with previous data
                    bool isEqual;
                    if (previousData is IEquatableObject equatableObject)
                    {// Use IEquatableObject if possible
                        isEqual = equatableObject.ObjectEquals(currentData);
                    }
                    else
                    {// Otherwise, compare serialized data
                        var (_, rentMemory) = TinyhandTypeIdentifier.TrySerializeRentMemory(this.TypeIdentifier, previousData);
                        isEqual = rentMemory.Span.SequenceEqual(result.Data.Span);
                        rentMemory.Return();
                    }

                    if (!isEqual)
                    {// Different data
                        return false;
                    }
                }
            }
            finally
            {
                result.Return();
            }

            var upplerLimit = i switch
            {
                0 => 0ul,
                1 => this.storageId0.JournalPosition,
                2 => this.storageId1.JournalPosition,
                _ => throw new InvalidOperationException(),
            };

            var plane = this.storageMap.CrystalObject is { } crystalObject ? crystalObject.Plane : 0;
            previousData = await journal.RestoreData(storageId.JournalPosition, upplerLimit, currentData, this.TypeIdentifier, plane, this.PointId).ConfigureAwait(false);
            if (previousData is null)
            {
                return false;
            }
        }

        return true;
    }

    #region IStructuralRoot

    bool IStructuralRoot.TryGetJournalWriter(JournalType recordType, out TinyhandWriter writer)
    {
        if (this.storageMap.CrystalControl?.Journal is { } journal &&
            this.storageMap.CrystalObject is { } crystalObject)
        {
            journal.GetWriter(recordType, out writer);

            writer.Write_Locator();
            writer.Write(crystalObject.Plane);
            writer.Write_Locator();
            writer.Write(this.pointId);
            return true;
        }
        else
        {
            writer = default;
            return false;
        }
    }

    ulong IStructuralRoot.AddJournalAndDispose(ref TinyhandWriter writer)
    {
        if (this.storageMap.CrystalControl?.Journal is { } journal)
        {
            return journal.Add(ref writer);
        }
        else
        {
            return 0;
        }
    }

    void IStructuralRoot.AddToSaveQueue(int delaySeconds)
    {
        // The delay time for Storage saving is configured collectively in StorageControl (CrystalControl.DefaultSaveDelaySeconds).
        /*if (this.storageMap.CrystalControl is { } crystalControl)
        {
            if (delaySeconds == 0)
            {
                delaySeconds = crystalControl.DefaultSaveDelaySeconds;
            }
        }*/

        if (this.saveQueueTime == 0)
        {
            this.storageControl.AddToSaveQueue(this);
        }
    }

    #endregion

    #region IStructuralObject

    internal async Task<bool> StoreData(StoreMode storeMode)
    {
        object? dataCopy;

        if (storeMode == StoreMode.TryRelease)
        {
            if (!this.TryEnter())
            {// Already locked
                return false;
            }

            try
            {
                this.storageControl.Release(this, false); // Release
                dataCopy = this.data;
                if (!this.IsPinned)
                {
                    this.data = default;
                }

                if (dataCopy is null)
                {// No data
                    return true;
                }
            }
            finally
            {
                this.Exit();
            }
        }
        else if (storeMode == StoreMode.ForceRelease)
        {
            await this.EnterAsync().ConfigureAwait(false);
            try
            {
                this.storageControl.Release(this, false); // Release
                dataCopy = this.data;
                if (!this.IsPinned)
                {
                    this.data = default;
                }

                if (dataCopy is null)
                {// No data
                    return true;
                }
            }
            finally
            {
                this.Exit();
            }
        }
        else
        {// Store data
            dataCopy = this.data;
            if (dataCopy is null)
            {// No data
                return true;
            }
        }

        bool result = true;

        if (!this.IsEnabled)
        {
            // Store children
            if (dataCopy is IStructuralObject structuralObject)
            {
                if (!await structuralObject.StoreData(storeMode).ConfigureAwait(false))
                {
                    result = false;
                }
            }

            return result;
        }

        // Serialize and get hash.
        (_, var rentMemory) = TinyhandTypeIdentifier.TrySerializeRentMemory(this.typeIdentifier, dataCopy);
        if (rentMemory.IsEmpty)
        {// No data
            return false;
        }

        // Store children (code is redundant because it is placed after serialization)
        if (dataCopy is IStructuralObject structuralObject2)
        {
            if (!await structuralObject2.StoreData(storeMode).ConfigureAwait(false))
            {
                result = false;
            }
        }

        var dataSize = rentMemory.Span.Length;
        var hash = FarmHash.Hash64(rentMemory.Span);
        if (storeMode == StoreMode.StoreOnly)
        {
            this.storageControl.SetStorageSize(this, dataSize);
        }

        if (hash != this.storageId0.Hash)
        {// Different data
            // Put
            ulong fileId = 0;
            this.storageMap.Storage.PutAndForget(ref fileId, rentMemory.ReadOnly);
            var currentPosition = this.storageMap.Journal is null ? Waypoint.ValidJournalPosition : this.storageMap.Journal.GetCurrentPosition();
            var storageId = new StorageId(currentPosition, fileId, hash);

            // Update storage id
            this.storageMap.StorageControl.AddStorage(this, this.storageMap.Storage, storageId);

            // Journal
            if (((IStructuralObject)this).TryGetJournalWriter(out var root, out var writer, true) == true)
            {
                writer.Write(JournalRecord.AddCustom);
                TinyhandSerializer.SerializeObject(ref writer, storageId);
                root.AddJournalAndDispose(ref writer);
            }
        }

        rentMemory.Return();

        return result;
    }

    void IStructuralObject.SetupStructure(IStructuralObject? parent, int key)
    {
        ((IStructuralObject)this).SetParentAndKey(parent, key);

        if (this.data is IStructuralObject structuralObject)
        {
            structuralObject.SetupStructure(this);
        }
    }

    internal Task DeleteData(DateTime forceDeleteAfter, bool writeJournal)
        => this.DeleteObject(forceDeleteAfter, true); // To record that a StoragePoint (StorageObject) has been deleted, the journal should be written except during journal replay.

    bool IStructuralObject.ProcessJournalRecord(ref TinyhandReader reader)
    {
        reader.TryPeekJournalRecord(out var record);
        if (record == JournalRecord.Key ||
            record == JournalRecord.Locator ||
            record == JournalRecord.AddItem ||
            record == JournalRecord.DeleteItem)
        {// Key or Locator
            this.PrepareForJournal();
            if (this.data is IStructuralObject structuralObject)
            {
                return structuralObject.ProcessJournalRecord(ref reader);
            }
            else
            {
                return false;
            }
        }

        if (record == JournalRecord.Value)
        {
            reader.Advance(1);
            this.data = TinyhandTypeIdentifier.TryDeserializeReader(this.TypeIdentifier, ref reader);
            return this.data is not null;
        }
        else if (record == JournalRecord.Delete)
        {// Delete storage
            reader.Advance(1);
            this.DeleteObject(default, false).ConfigureAwait(false).GetAwaiter().GetResult();
            return true;
        }
        else if (record == JournalRecord.AddCustom)
        {
            reader.Advance(1);
            var storageId = TinyhandSerializer.DeserializeObject<StorageId>(ref reader);
            if (storageId > this.storageId0)
            {
                this.storageMap.StorageControl.AddStorage(this, this.storageMap.Storage, storageId);
            }

            return true;
        }

        return false;
    }

    void IStructuralObject.WriteLocator(ref TinyhandWriter writer)
    {
        writer.Write_Locator();
        writer.Write(this.pointId);
    }

    #endregion

    private async Task PrepareAndLoadInternal<TData>()
        where TData : class
    {// Lock:this
        if (this.data is not null)
        {// Already loaded
            return;
        }

        var storage = this.storageMap.Storage;
        ulong fileId = 0;
        ulong journalPosition = 0;
        CrystalMemoryOwnerResult result = new(CrystalResult.NotFound);
        while (this.storageId0.IsValid)
        {
            fileId = this.storageId0.FileId;
            result = await storage.GetAsync(ref fileId).ConfigureAwait(false);
            if (result.IsSuccess &&
                FarmHash.Hash64(result.Data.Span) == this.storageId0.Hash)
            {
                break;
            }

            journalPosition = this.storageId1.JournalPosition;
            this.storageId0 = this.storageId1;
            this.storageId1 = this.storageId2;
            this.storageId2 = default;
        }

        if (result.IsFailure)
        {
            return;
        }

        // Deserialize
        try
        {
            TData? data;
            if (typeof(TData) == typeof(object))
            {// // If the type is object, use the TypeIdentifier instead.
                data = (TData?)TinyhandTypeIdentifier.TryDeserialize(this.TypeIdentifier, result.Data.Span);
            }
            else
            {
                TinyhandSerializer.TryDeserialize<TData>(result.Data.Span, out data);
            }

            if (data is null)
            {
                return;
            }

            if (journalPosition > 0)
            {// Since the storage was lost, attempt to reconstruct it using the existing data and the journal.
                TData? restoredData = default;
                if (this.storageMap.Journal is { } journal)
                {
                    var plane = this.storageMap.CrystalObject is { } crystalObject ? crystalObject.Plane : 0;
                    restoredData = await journal.RestoreData<TData>(journalPosition, 0ul, data, plane, this.PointId).ConfigureAwait(false) as TData;
                }

                var dataType = data.GetType();
                var storageInfo = $"Type = {dataType.Name}, PointId = {this.pointId}";

                if (restoredData is not null)
                {// Successfully restored
                    data = restoredData;
                    this.storageControl.Logger?.TryGet(LogLevel.Warning)?.Log(CrystalDataHashed.StorageControl.StorageReconstructed, storageInfo);
                }
                else
                {// Could not restore
                    this.storageControl.Logger?.TryGet(LogLevel.Error)?.Log(CrystalDataHashed.StorageControl.StorageNotReconstructed, storageInfo);
                }
            }

            this.SetDataInternal(data, false, result.Data);
        }
        finally
        {
            result.Return();
        }
    }

    private void PrepareForJournal()
    {// Lock:?
        if (this.data is not null)
        {// Already loaded
            return;
        }

        var storage = this.storageMap.Storage;
        ulong fileId = 0;
        if (this.storageId0.IsValid)
        {
            fileId = this.storageId0.FileId;
            var result = storage.GetAsync(ref fileId).ConfigureAwait(false).GetAwaiter().GetResult();
            if (result.IsSuccess &&
                FarmHash.Hash64(result.Data.Span) == this.storageId0.Hash)
            {
                this.data = TinyhandTypeIdentifier.TryDeserialize(this.TypeIdentifier, result.Data.Span);
            }

            result.Return();
        }

        if (this.data is null)
        {
            this.data = TinyhandTypeIdentifier.TryReconstruct(this.TypeIdentifier);
        }

        if (this.data is IStructuralObject structuralObject)
        {
            structuralObject.SetupStructure(this);
        }
    }

    internal void SetTypeIdentifier<TData>()
        where TData : class
        => this.typeIdentifier = TinyhandTypeIdentifier.GetTypeIdentifier<TData>();

    [MemberNotNull(nameof(data))]
    internal void SetDataInternal<TData>(TData newData, bool recordJournal, BytePool.RentReadOnlyMemory original)
        where TData : class
    {// Lock:this
        if (this.IsPinned)
        {// Pinned (data is guaranteed to be non-null)
#pragma warning disable CS8774 // Member must have a non-null value when exiting.
            return;
#pragma warning restore CS8774 // Member must have a non-null value when exiting.
        }

        BytePool.RentMemory rentMemory = default;
        this.data = newData!;
        if (this.data is IStructuralObject structuralObject)
        {
            structuralObject.SetupStructure(this);
        }

        if (this.IsEnabled)
        {
            if (original.IsEmpty)
            {
                if (typeof(TData) == typeof(object))
                {
                    (_, rentMemory) = TinyhandTypeIdentifier.TrySerializeRentMemory(this.TypeIdentifier, newData!);
                }
                else
                {
                    rentMemory = TinyhandSerializer.SerializeToRentMemory(newData);
                }

                original = rentMemory.ReadOnly;
            }

            this.storageControl.MoveToRecent(this, original.Length);
        }

        if (recordJournal &&
            ((IStructuralObject)this).TryGetJournalWriter(out var root, out var writer, true) == true)
        {
            if (original.IsEmpty)
            {
                if (typeof(TData) == typeof(object))
                {
                    (_, rentMemory) = TinyhandTypeIdentifier.TrySerializeRentMemory(this.TypeIdentifier, newData!);
                }
                else
                {
                    rentMemory = TinyhandSerializer.SerializeToRentMemory(newData);
                }

                original = rentMemory.ReadOnly;
            }

            writer.Write(JournalRecord.Value);
            writer.WriteSpan(original.Span);
            root.AddJournalAndDispose(ref writer);
        }

        if (rentMemory.IsRent)
        {
            rentMemory.Return();
        }
    }

    private async Task DeleteObject(DateTime forceDeleteAfter, bool writeJournal)
    {
        await this.EnterAsync().ConfigureAwait(false);
        try
        {
            var dataToDelete = this.data;
            this.data = default;

            if (dataToDelete is null &&
                this.storageId0.IsValid)
            {// Load the data and delete child objects.
                var fileId = this.storageId0.FileId;
                var result = await this.storageMap.Storage.GetAsync(ref fileId).ConfigureAwait(false);
                if (result.IsSuccess &&
                    FarmHash.Hash64(result.Data.Span) == this.storageId0.Hash)
                {
                    dataToDelete = TinyhandTypeIdentifier.TryDeserialize(this.TypeIdentifier, result.Data.Span);
                }
            }

            if (dataToDelete is IStructuralObject structuralObject)
            {
                await structuralObject.DeleteData(forceDeleteAfter, false).ConfigureAwait(false);
            }
        }
        finally
        {
            this.Exit();
        }

        this.storageControl.EraseStorage(this);

        if (writeJournal)
        {
            ((IStructuralObject)this).AddJournalRecord(JournalRecord.Delete);
        }
    }

    /*internal bool Compare(StorageObject other)
    {
        if (this.pointId != other.pointId ||
            this.typeIdentifier != other.typeIdentifier)
        {
            return false;
        }

        if (this.storageId0.Equals(ref other.storageId0))
        {
            return true;
        }

        this.PrepareForJournal();
        other.PrepareForJournal();
        if (this.data is null || other.data is null)
        {
            return false;
        }

        var (_, r1) = TinyhandTypeIdentifier.TrySerializeRentMemory(this.TypeIdentifier, this.data);
        var (_, r2) = TinyhandTypeIdentifier.TrySerializeRentMemory(other.TypeIdentifier, other.data);
        var result = r1.Span.SequenceEqual(r2.Span);
        r1.Return();
        r2.Return();

        return result;
    }*/
}
