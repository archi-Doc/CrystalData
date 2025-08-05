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
    private const uint PendingReleaseStateBit = 1u << 29;
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

#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
#pragma warning restore SA1401 // Fields should be private

    private object? data; // Lock:this
    private uint state; // Lock:this
    private int size; // Lock:this

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
        get => default;
        set { }
    }

    public ulong PointId => this.pointId;

    public uint TypeIdentifier => this.typeIdentifier;

    public int Size => this.size;

    internal StorageControl storageControl => this.storageMap.StorageControl;

    public bool IsDisabled => (this.state & DisabledStateBit) != 0;

    // public new bool IsLocked => (this.state & LockedStateBit) != 0;

    public bool IsRip => (this.state & RipStateBit) != 0;

    public bool IsPendingRelease => (this.state & PendingReleaseStateBit) != 0;

    // public bool IsPendingRip => (this.state & PendingRipStateBit) != 0;

    public bool CanUnload => !this.IsLocked;

    #endregion

    public StorageObject()
    {
        this.storageMap = StorageControl.Default.DisabledMap;
    }

    internal void Initialize(ulong pointId, uint typeIdentifier, StorageMap storageMap)
    {// Lock:StorageControl
        this.pointId = pointId;
        this.typeIdentifier = typeIdentifier;
        this.storageMap = storageMap;
        if (storageMap.IsDisabled)
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

        if ((this.storageMap.IsDisabled || this.IsDisabled) && this.data is not null)
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
            this.storageControl.MoveToRecent(this, 0);
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
                this.SetDataInternal(TinyhandSerializer.Reconstruct<TData>(), false);
            }

            return (TData)this.data;
        }
        finally
        {
            this.Exit();
        }
    }

    internal async ValueTask<TData?> Get<TData>()
    {
        if (this.data is { } data)
        {
            this.storageControl.MoveToRecent(this, 0);
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

    /*internal async ValueTask<TData?> TryLock<TData>()
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
            this.SetDataInternal(TinyhandSerializer.Reconstruct<TData>(), false);
        }

        return (TData?)this.data;
    }

    internal void Unlock()
    {// Lock:this
        this.ReleaseIfPendingInternal();
        this.Exit();
    }*/

    internal void Set<TData>(TData data)
        where TData : notnull
    {
        using (this.Lock())
        {
            this.SetDataInternal(data, true);
        }
    }

    #region IStructualObject

    internal async Task<bool> StoreData(StoreMode storeMode)
    {
        if (this.data is not { } data ||
            this.StructualRoot is not ICrystal crystal)
        {// No data
            return true;
        }

        /*this.SetPendingReleaseStateBit();
        if (storeMode == StoreMode.Release)
        {// Release
            this.SetPendingReleaseStateBit();
        }

        bool result;
        if (this.TryEnter())
        {
            try
            {
                result = await this.StoreData(storeMode, data, crystal).ConfigureAwait(false);
                this.ReleaseIfPendingInternal();
            }
            finally
            {
                this.Exit();
            }
        }*/

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
    }

    void IStructualObject.SetupStructure(IStructualObject? parent, int key)
    {
        ((IStructualObject)this).SetParentAndKey(parent, key);

        if (this.data is IStructualObject structualObject)
        {
            structualObject.SetupStructure(this, 0);
        }
    }

    internal void Erase()
    {
        this.EraseStorage(true);
    }

    bool IStructualObject.ReadRecord(ref TinyhandReader reader)
    {
        if (!reader.TryPeek(out JournalRecord record))
        {
            return false;
        }

        if (record == JournalRecord.EraseStorage)
        {// Erase storage
            this.EraseStorage(false);
            return true;
        }
        else if (record == JournalRecord.AddStorage)
        {
            if (((IStructualObject)this).StructualRoot is not ICrystal crystal)
            {// No crystal
                return true;
            }

            reader.TryRead(out record);
            var storageId = TinyhandSerializer.DeserializeObject<StorageId>(ref reader);
            StorageControl.Default.AddStorage(this, crystal, storageId);
            return true;
        }

        if (this.data is null)
        {
            this.data = this.Get<object>().Result;
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
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReleaseIfPendingInternal()
    {// Lock:this
        if (this.IsPendingRelease)
        {
            this.ClearPendingReleaseStateBit();
            this.storageControl.Release(this, false);
            this.data = default;
        }
    }

    private async Task<bool> StoreData(StoreMode storeMode, object data, ICrystal crystal)
    {// No lock
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

        if (hash != this.storageId0.Hash)
        {// Different data
            // Put
            ulong fileId = 0;
            crystal.Storage.PutAndForget(ref fileId, rentMemory.ReadOnly);
            var currentPosition = crystal.Journal is null ? Waypoint.ValidJournalPosition : crystal.Journal.GetCurrentPosition();
            var storageId = new StorageId(currentPosition, fileId, hash);

            // Update storage id
            StorageControl.Default.AddStorage(this, crystal, storageId);

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
            var data = TinyhandSerializer.Deserialize<TData>(result.Data.Span);
            if (data is not null)
            {
                this.SetDataInternal(data, false);
            }
        }
        finally
        {
            result.Return();
        }
    }

    [MemberNotNull(nameof(data))]
    internal void SetDataInternal<TData>(TData data, bool recordJournal)
    {// Lock:this
        BytePool.RentMemory rentMemory = default;

        this.data = data!;
        if (this.data is IStructualObject structualObject)
        {
            structualObject.SetupStructure(this);
        }

        if (this.storageMap.IsEnabled)
        {
            rentMemory = TinyhandSerializer.SerializeToRentMemory(data);
            this.storageControl.MoveToRecent(this, rentMemory.Length - this.size);
            this.size = rentMemory.Length;
        }

        if (recordJournal &&
            ((IStructualObject)this).TryGetJournalWriter(out var root, out var writer, true) == true)
        {
            writer.Write(JournalRecord.Value);
            writer.Write(rentMemory.Span);
            root.AddJournal(ref writer);
        }

        if (rentMemory.IsRent)
        {
            rentMemory.Return();
        }
    }

    private void EraseStorage(bool recordJournal)
    {
        this.storageControl.EraseStorage(this);

        using (this.Lock())
        {
            if (this.data is IStructualObject structualObject)
            {
                structualObject.Erase();
            }
        }

        if (recordJournal)
        {
            ((IStructualObject)this).AddJournalRecord(JournalRecord.EraseStorage);
        }
    }

    private void SetDisableStateBit() => this.state |= DisabledStateBit;

    private void ClearDisableStateBit() => this.state &= ~DisabledStateBit;

    private void SetRipStateBit() => this.state |= RipStateBit;

    private void ClearRipStateBit() => this.state &= ~RipStateBit;

    private void SetPendingReleaseStateBit() => this.state |= PendingReleaseStateBit;

    private void ClearPendingReleaseStateBit() => this.state &= ~PendingReleaseStateBit;

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
        this.Enter(); // using (this.Lock())

        if (disableStorage)
        {
            this.SetDisableStateBit();
        }
        else
        {// Enable storage
            if (this.storageMap.IsEnabled)
            {
                this.ClearDisableStateBit();
            }
        }

        this.Exit();
    }
}
