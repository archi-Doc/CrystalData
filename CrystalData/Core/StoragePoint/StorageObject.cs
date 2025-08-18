// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Tinyhand.IO;

namespace CrystalData.Internal;

#pragma warning disable SA1202 // Elements should be ordered by access

[TinyhandObject(ExplicitKeyOnly = true)]
[ValueLinkObject]
public sealed partial class StorageObject : SemaphoreLock, IStructualObject
{// Disabled, Rip, PendingRelease
    public const int MaxHistories = 3; // 4

    private const uint DisabledStateBit = 1u << 31;
    private const uint RipStateBit = 1u << 30;
    // private const uint ValueSetStateBit = 1u << 29;
    // private const uint PendingReleaseStateBit = 1u << 29;
    // private const uint PendingRipStateBit = 1u << 28;
    // private const uint LockedStateBit = 1u << 0;

    #region FieldAndProperty

#pragma warning disable SA1401 // Fields should be private
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter

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
    internal StorageObject? previous; // Lock:StorageControl
    internal StorageObject? next; // Lock:StorageControl

    private object? data; // Lock:this
    private uint state; // Lock:StorageControl
    internal int size; // Lock:StorageControl

#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
#pragma warning restore SA1401 // Fields should be private

    public IStructualRoot? StructualRoot
    {
        get => ((IStructualObject)this.storageMap).StructualRoot;
        set { }
    }

    public IStructualObject? StructualParent
    {
        get => this.storageMap;
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

    public bool IsDisabled => (this.state & DisabledStateBit) != 0;

    // public new bool IsLocked => (this.state & LockedStateBit) != 0;

    public bool IsRip => (this.state & RipStateBit) != 0;

    // public bool IsPendingRelease => (this.state & PendingReleaseStateBit) != 0;

    // public bool IsPendingRip => (this.state & PendingRipStateBit) != 0;

    #endregion

    public StorageObject()
    {
        this.storageMap = StorageMap.Disabled;
    }

    internal void Initialize(ulong pointId, uint typeIdentifier, StorageMap storageMap)
    {// Lock:StorageControl
        this.pointId = pointId;
        this.typeIdentifier = typeIdentifier;
        this.storageMap = storageMap;
        if (!storageMap.IsEnabled)
        {// Disable storage
            this.SetDisableStateBit();
        }
    }

    internal void SerializeStoragePoint(ref TinyhandWriter writer, TinyhandSerializerOptions options)
    {
        if (options.IsSignatureMode)
        {// Signature
            writer.Write(0x8bc0a639u);
            writer.Write(this.pointId);
            return;
        }

        if ((!this.storageMap.IsEnabled || this.IsDisabled) && this.data is not null)
        {// Storage disabled
            TinyhandTypeIdentifier.TrySerializeWriter(ref writer, this.typeIdentifier, this.data, options);
        }
        else
        {// In-class
            writer.Write(this.pointId);
        }
    }

    internal async ValueTask<TData> GetOrCreate<TData>()
    {
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
    }

    internal async ValueTask<TData?> TryGet<TData>()
    {
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

            return (TData?)this.data;
        }
        finally
        {
            this.Exit();
        }
    }

    internal async ValueTask<DataScope<TData>> EnterScope<TData>()
        where TData : notnull
    {
        if (this.storageControl.IsRip || this.IsRip)
        {
            return default;
        }

        await this.EnterAsync().ConfigureAwait(false);
        if (this.storageControl.IsRip || this.IsRip)
        {
            this.Exit();
            return default;
        }

        if (this.data is null)
        {// PrepareAndLoad
            await this.PrepareAndLoadInternal<TData>().ConfigureAwait(false);
        }

        if (this.data is null)
        {// Reconstruct
            this.SetDataInternal(TinyhandSerializer.Reconstruct<TData>(), false, default);
        }

        return new DataScope<TData>(this, (TData)this.data);
    }

    internal async ValueTask<TData?> TryLock<TData>()
    {
        if (this.storageControl.IsRip || this.IsRip)
        {
            return default;
        }

        await this.EnterAsync().ConfigureAwait(false);
        if (this.storageControl.IsRip || this.IsRip)
        {
            this.Exit();
            return default;
        }

        if (this.data is null)
        {// PrepareAndLoad
            await this.PrepareAndLoadInternal<TData>().ConfigureAwait(false);
        }

        if (this.data is null)
        {// Reconstruct
            this.SetDataInternal(TinyhandSerializer.Reconstruct<TData>(), false, default);
        }

        return (TData?)this.data;
    }

    internal void Unlock()
    {// Lock:this
        // this.ReleaseIfPendingInternal();
        this.Exit();
    }

    internal void Set<TData>(TData data)
        where TData : notnull
    {
        using (this.EnterScope())
        {
            this.SetDataInternal(data, true, default);
        }
    }

    #region IStructualObject

    internal async Task<bool> StoreData(StoreMode storeMode)
    {
        if (this.data is null ||
            this.StructualRoot is not ICrystal)
        {// No dataqqq
            return true;
        }

        object? data;
        ICrystal? crystal;

        if (storeMode == StoreMode.TryRelease)
        {
            if (!this.TryEnter())
            {// Already locked
                return false;
            }

            try
            {
                data = this.data;
                crystal = this.StructualRoot as ICrystal;
                if (data is null || crystal is null)
                {// No data
                    return true;
                }

                // Release
                this.storageControl.Release(this, false);
                this.data = default;
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
                data = this.data;
                crystal = this.StructualRoot as ICrystal;
                if (data is null || crystal is null)
                {// No data
                    return true;
                }

                // Release
                this.storageControl.Release(this, false);
                this.data = default;
            }
            finally
            {
                this.Exit();
            }
        }
        else
        {// Store data
            data = this.data;
            crystal = this.StructualRoot as ICrystal;
            if (data is null || crystal is null)
            {// No data
                return true;
            }
        }

        bool result = true;

        // Store children
        if (data is IStructualObject structualObject)
        {
            if (!await structualObject.StoreData(storeMode).ConfigureAwait(false))
            {
                result = false;
            }
        }

        // Serialize and get hash.
        (_, var rentMemory) = TinyhandTypeIdentifier.TrySerializeRentMemory(this.typeIdentifier, data);
        if (rentMemory.IsEmpty)
        {// No data
            return false;
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
            crystal.Storage.PutAndForget(ref fileId, rentMemory.ReadOnly);
            var currentPosition = crystal.Journal is null ? Waypoint.ValidJournalPosition : crystal.Journal.GetCurrentPosition();
            var storageId = new StorageId(currentPosition, fileId, hash);

            // Update storage id
            this.storageMap.StorageControl.AddStorage(this, crystal, storageId);

            // Journal
            if (((IStructualObject)this).TryGetJournalWriter(out var root, out var writer, true) == true)
            {
                writer.Write(JournalRecord.AddStorage);
                TinyhandSerializer.SerializeObject(ref writer, storageId);
                root.AddJournal(ref writer);
            }
        }

        rentMemory.Return();

        return result;
    }

    /*internal async Task<bool> StoreData(StoreMode storeMode)
    {
        if (this.data is not { } data ||
            this.StructualRoot is not ICrystal crystal)
        {// No data
            return true;
        }

        // this.SetPendingReleaseStateBit();
        // if (storeMode == StoreMode.Release)
        // {// Release
        //    this.SetPendingReleaseStateBit();
        // }

        // bool result;
        // if (this.TryEnter())
        // {
        //    try
        //    {
        //        result = await this.StoreData(storeMode, data, crystal).ConfigureAwait(false);
        //        this.ReleaseIfPendingInternal();
        //    }
        //    finally
        //    {
        //        this.Exit();
        //    }
        // }

        await this.StoreData(storeMode, data, crystal).ConfigureAwait(false);

        if (storeMode == StoreMode.Release)
        {// Release
            this.SetPendingReleaseStateBit();
            if (this.TryEnter())
            {
                try
                {
                    this.ReleaseIfPendingInternal();
                }
                finally
                {
                    this.Exit();
                }
            }
        }

        return true;
    }*/

    void IStructualObject.SetupStructure(IStructualObject? parent, int key)
    {
        ((IStructualObject)this).SetParentAndKey(parent, key);

        if (this.data is IStructualObject structualObject)
        {
            structualObject.SetupStructure(this);
        }
    }

    internal void Delete()
    {
        this.DeleteStorage(true);
    }

    bool IStructualObject.ReadRecord(ref TinyhandReader reader)
    {
        if (!reader.TryReadJournalRecord(out JournalRecord record))
        {
            return false;
        }

        if (record == JournalRecord.EraseStorage)
        {// Delete storage
            this.DeleteStorage(false);
            return true;
        }
        else if (record == JournalRecord.Value)
        {
            this.data ??= TinyhandTypeIdentifier.TryDeserializeReader(this.TypeIdentifier, ref reader);
            return this.data is not null;
        }
        else if (record == JournalRecord.AddStorage)
        {
            if (((IStructualObject)this).StructualRoot is not ICrystal crystal)
            {// No crystal
                return true;
            }

            var storageId = TinyhandSerializer.DeserializeObject<StorageId>(ref reader);
            this.storageMap.StorageControl.AddStorage(this, crystal, storageId);
            return true;
        }

        if (this.data is IStructualObject structualObject)
        {
            return structualObject.ReadRecord(ref reader);
        }
        else
        {
            return false;
        }
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

        if (((IStructualObject)this).StructualRoot is not ICrystal crystal)
        {// No crystal
            return;
        }

        var storage = crystal.Storage;
        ulong fileId = 0;
        CrystalMemoryOwnerResult result = new(CrystalResult.NotFound);
        while (this.storageId0.IsValid)
        {
            fileId = this.storageId0.FileId;
            result = await storage.GetAsync(ref fileId).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                break;
            }

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
                if (TinyhandTypeIdentifier.TryDeserialize(this.TypeIdentifier, result.Data.Span) is { } obj)
                {
                    data = (TData)obj;
                }
                else
                {
                    return;
                }
            }
            else
            {
                data = TinyhandSerializer.Deserialize<TData>(result.Data.Span);
            }

            this.SetDataInternal(data, false, result.Data);
        }
        finally
        {
            result.Return();
        }
    }

    [MemberNotNull(nameof(data))]
    internal void SetDataInternal<TData>(TData data, bool recordJournal, BytePool.RentReadOnlyMemory original)
    {// Lock:this
        BytePool.RentMemory rentMemory = default;
        this.data = data!;
        if (this.data is IStructualObject structualObject)
        {
            structualObject.SetupStructure(this);
        }

        if (this.storageMap.IsEnabled)
        {
            if (original.IsEmpty)
            {
                if (typeof(TData) == typeof(object))
                {
                    (_, rentMemory) = TinyhandTypeIdentifier.TrySerializeRentMemory(this.TypeIdentifier, data!);
                }
                else
                {
                    rentMemory = TinyhandSerializer.SerializeToRentMemory(data);
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
                    (_, rentMemory) = TinyhandTypeIdentifier.TrySerializeRentMemory(this.TypeIdentifier, data!);
                }
                else
                {
                    rentMemory = TinyhandSerializer.SerializeToRentMemory(data);
                }

                original = rentMemory.ReadOnly;
            }

            writer.Write(JournalRecord.Value);
            writer.WriteSpan(original.Span);
            root.AddJournal(ref writer);
        }

        if (rentMemory.IsRent)
        {
            rentMemory.Return();
        }
    }

    private void DeleteStorage(bool recordJournal)
    {
        this.storageControl.EraseStorage(this);

        using (this.EnterScope())
        {
            if (this.data is IStructualObject structualObject)
            {
                structualObject.Delete();
            }

            this.data = default;
        }

        if (recordJournal)
        {
            ((IStructualObject)this).AddJournalRecord(JournalRecord.EraseStorage);
        }
    }

    internal void SetDisableStateBit() => this.state |= DisabledStateBit;

    internal void ClearDisableStateBit() => this.state &= ~DisabledStateBit;

    /*internal void SetValueSetStateBit() => this.state |= ValueSetStateBit;

    internal void ClearValueSetStateBit() => this.state &= ~ValueSetStateBit;

    internal bool CheckValueSetStateBit => (this.state & ValueSetStateBit) != 0;*/

    /*private void SetRipStateBit() => this.state |= RipStateBit;

    private void ClearRipStateBit() => this.state &= ~RipStateBit;

    private void SetPendingReleaseStateBit() => this.state |= PendingReleaseStateBit;

    private void ClearPendingReleaseStateBit() => this.state &= ~PendingReleaseStateBit;*/

    /*private bool TryLockInternal()
    {
        if (this.IsLocked)
        {
            return false;
        }
        else
        {
            this.state |= LockedStateBit;
            return true;
        }
    }

    private void UnlockInternal()
    {
        this.state &= ~LockedStateBit;
    }*/

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ConfigureStorage(bool disableStorage)
    {
        this.storageControl.ConfigureStorage(this, disableStorage);
    }
}
