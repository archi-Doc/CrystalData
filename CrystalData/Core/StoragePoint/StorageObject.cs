// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Tinyhand.IO;

namespace CrystalData.Internal;

#pragma warning disable SA1202 // Elements should be ordered by access
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1401 // Fields should be private

[TinyhandObject(ExplicitKeyOnly = true)]
[ValueLinkObject]
public sealed partial class StorageObject : SemaphoreLock, IStructualObject, IStructualRoot, IDataUnlocker
{
    public const int MaxHistories = 3;

    #region FieldAndProperty

    internal ObjectProtectionState protectionState;

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

    public IStructualRoot? StructualRoot
    {
        get => this;
        // get => ((IStructualObject)this.storageMap).StructualRoot;
        set { }
    }

    public IStructualObject? StructualParent
    {
        get => default;
        // get => this.storageMap;
        set { }
    }

    public int StructualKey
    {
        get => -1;
        set { }
    }

    public ulong PointId => this.pointId;

    public uint TypeIdentifier => this.typeIdentifier;

    public int Size => this.size;

    internal StorageControl storageControl => this.storageMap.StorageControl;

    public bool IsEnabled => this.storageMap.IsEnabled;

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

    /*internal async ValueTask<TData> GetOrCreate<TData>()
    {// Even if creation is attempted, the object may already have been deleted (ObjectProtectionState.Deleted), so this function was abandoned.
        if (this.data is { } data)
        {
            this.storageControl.MoveToRecent(this);
            return (TData)data;
        }

        await this.EnterAsync().ConfigureAwait(false);
        try
        {
            if (this.data is null)
            {// PrepareAndLoad
                await this.PrepareAndLoadInternal<TData>().ConfigureAwait(false);
            }

            if (this.data is null)
            {// Reconstruct
                if (typeof(TData) == typeof(object))
                {// If the type is object, use the TypeIdentifier instead.
                    this.SetDataInternal(TinyhandTypeIdentifier.TryReconstruct(this.TypeIdentifier), false, default);
                }
                else
                {
                    this.SetDataInternal(TinyhandSerializer.Reconstruct<TData>(), false, default);
                }
            }

            return (TData)this.data;
        }
        finally
        {
            this.Exit();
        }
    }*/

    internal async ValueTask<TData?> TryGet<TData>(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (this.data is { } data)
        {
            this.storageControl.MoveToRecent(this);
            return (TData)data;
        }

        if (!await this.EnterAsync(timeout, cancellationToken).ConfigureAwait(false))
        {// Timeout or cancellation
            return default;
        }

        try
        {
            if (this.protectionState == ObjectProtectionState.Deleted)
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

    internal async ValueTask<DataScope<TData>> TryLock<TData>(AcquisitionMode acquisitionMode, TimeSpan timeout, CancellationToken cancellationToken)
        where TData : notnull
    {
        if (this.storageControl.IsRip)
        {
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
        if (Interlocked.CompareExchange(ref this.protectionState, ObjectProtectionState.Protected, ObjectProtectionState.Unprotected) != ObjectProtectionState.Unprotected)
        {// Protected(?) or Deleted
            this.Exit();
            return new(DataScopeResult.Obsolete);
        }

        if (this.data is null)
        {// PrepareAndLoad
            await this.PrepareAndLoadInternal<TData>().ConfigureAwait(false);
        }

        if (this.data is not null)
        {// Already exists
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

        return new((TData)this.data, this);
    }

    public void Unlock()
    {// Lock:this
        // Protected -> Unprotected
        Interlocked.CompareExchange(ref this.protectionState, ObjectProtectionState.Unprotected, ObjectProtectionState.Protected);

        this.Exit();
    }

    internal void DeleteLatestStorageForTest()
    {
        this.storageControl.DeleteLatestStorageForTest(this);
    }

    internal void Set<TData>(TData data)
        where TData : notnull
    {
        using (this.EnterScope())
        {
            if (this.protectionState != ObjectProtectionState.Unprotected)
            {
                return;
            }

            this.SetDataInternal(data, true, default);
        }
    }

    #region IStructualRoot

    bool IStructualRoot.TryGetJournalWriter(JournalType recordType, out TinyhandWriter writer)
    {
        if (this.storageMap.Crystalizer?.Journal is { } journal &&
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

    ulong IStructualRoot.AddJournalAndDispose(ref TinyhandWriter writer)
    {
        if (this.storageMap.Crystalizer?.Journal is { } journal)
        {
            return journal.Add(ref writer);
        }
        else
        {
            return 0;
        }
    }

    void IStructualRoot.AddToSaveQueue(int delaySeconds)
    {
        // The delay time for Storage saving is configured collectively in StorageControl (Crystalizer.DefaultSaveDelaySeconds).
        /*if (this.storageMap.Crystalizer is { } crystalizer)
        {
            if (delaySeconds == 0)
            {
                delaySeconds = crystalizer.DefaultSaveDelaySeconds;
            }
        }*/

        if (this.saveQueueTime == 0)
        {
            this.storageControl.AddToSaveQueue(this);
        }
    }

    #endregion

    #region IStructualObject

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
                this.data = default;
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
                this.data = default;
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
            if (dataCopy is IStructualObject structualObject)
            {
                if (!await structualObject.StoreData(storeMode).ConfigureAwait(false))
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
        if (dataCopy is IStructualObject structualObject2)
        {
            if (!await structualObject2.StoreData(storeMode).ConfigureAwait(false))
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
            if (((IStructualObject)this).TryGetJournalWriter(out var root, out var writer, true) == true)
            {
                writer.Write(JournalRecord.AddItem);
                TinyhandSerializer.SerializeObject(ref writer, storageId);
                root.AddJournalAndDispose(ref writer);
            }
        }

        rentMemory.Return();

        return result;
    }

    void IStructualObject.SetupStructure(IStructualObject? parent, int key)
    {
        ((IStructualObject)this).SetParentAndKey(parent, key);

        if (this.data is IStructualObject structualObject)
        {
            structualObject.SetupStructure(this);
        }
    }

    internal Task Delete(DateTime forceDeleteAfter)
        => this.DeleteStorage(true, forceDeleteAfter);

    bool IStructualObject.ProcessJournalRecord(ref TinyhandReader reader)
    {
        if (reader.TryReadJournalRecord_PeekIfKeyOrLocator(out var record))
        {// Key or Locator
            if (this.data is IStructualObject structualObject)
            {
                return structualObject.ProcessJournalRecord(ref reader);
            }
            else
            {
                return false;
            }
        }

        if (record == JournalRecord.Value)
        {
            this.data = TinyhandTypeIdentifier.TryDeserializeReader(this.TypeIdentifier, ref reader);
            return this.data is not null;
        }
        else if (record == JournalRecord.Delete)
        {// Delete storage
            this.DeleteStorage(false, default).Wait();
            return true;
        }
        else if (record == JournalRecord.AddItem)
        {
            var storageId = TinyhandSerializer.DeserializeObject<StorageId>(ref reader);
            if (storageId > this.storageId0)
            {
                this.storageMap.StorageControl.AddStorage(this, this.storageMap.Storage, storageId);
            }

            return true;
        }

        return false;
    }

    void IStructualObject.WriteLocator(ref TinyhandWriter writer)
    {
        writer.Write_Locator();
        writer.Write(this.pointId);
    }

    #endregion

    private async Task PrepareAndLoadInternal<TData>()
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
            if (result.IsSuccess)
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

            this.SetDataInternal(data, false, result.Data);
            if (journalPosition > 0)
            {// Since the storage was lost, attempt to reconstruct it using the existing data and the journal.
                var restoreResult = false;
                if (data is not null &&
                    this.storageMap.Journal is { } journal)
                {
                    var plane = this.storageMap.CrystalObject is { } crystalObject ? crystalObject.Plane : 0;
                    restoreResult = await journal.RestoreData<TData>(journalPosition, data, plane, this.PointId).ConfigureAwait(false);
                }

                var dataType = this.data.GetType();
                var storageInfo = $"Type = {dataType.Name}, PointId = {this.pointId}";

                if (restoreResult)
                {// Successfully restored
                    this.storageControl.Logger?.TryGet(LogLevel.Warning)?.Log(CrystalDataHashed.StorageControl.StorageReconstructed, storageInfo);
                }
                else
                {// Could not restore
                    this.storageControl.Logger?.TryGet(LogLevel.Error)?.Log(CrystalDataHashed.StorageControl.StorageNotReconstructed, storageInfo);
                }
            }
        }
        finally
        {
            result.Return();
        }
    }

    internal void SetTypeIdentifier<TData>()
        => this.typeIdentifier = TinyhandTypeIdentifier.GetTypeIdentifier<TData>();

    [MemberNotNull(nameof(data))]
    internal void SetDataInternal<TData>(TData newData, bool recordJournal, BytePool.RentReadOnlyMemory original)
    {// Lock:this
        BytePool.RentMemory rentMemory = default;
        this.data = newData!;
        if (this.data is IStructualObject structualObject)
        {
            structualObject.SetupStructure(this);
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
            ((IStructualObject)this).TryGetJournalWriter(out var root, out var writer, true) == true)
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

    private async Task DeleteStorage(bool recordJournal, DateTime forceDeleteAfter)
    {
        this.storageControl.EraseStorage(this);

        await this.EnterAsync().ConfigureAwait(false);
        try
        {
            if (this.data is IStructualObject structualObject)
            {
                await structualObject.Delete(forceDeleteAfter).ConfigureAwait(false);
            }

            this.data = default;
        }
        finally
        {
            this.Exit();
        }

        if (recordJournal)
        {
            ((IStructualObject)this).AddJournalRecord(JournalRecord.Delete);
        }
    }
}
