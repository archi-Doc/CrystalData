// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Runtime.CompilerServices;
using Tinyhand;
using Tinyhand.IO;

namespace CrystalData.Internal;

#pragma warning disable SA1202 // Elements should be ordered by access

[TinyhandObject(ExplicitKeyOnly = true)]
[ValueLinkObject]
public sealed partial class StorageObject : SemaphoreLock, IStructualObject
{// Disabled, Rip, PendingRelease, Locked
    public const int MaxHistories = 3; // 4

    private const uint DisabledStateBit = 1u << 31;
    private const uint RipStateBit = 1u << 30;
    private const uint PendingReleaseStateBit = 1u << 29;
    // private const uint PendingRipStateBit = 1u << 28;
    private const uint LockedStateBit = 1u << 0;

    #region FieldAndProperty

    [Key(0)]
    [Link(Primary = true, Unique = true, Type = ChainType.Unordered, AddValue = false)]
    private ulong pointId; // Lock:StorageControl, Key:0

    [Key(1)]
    private uint typeIdentifier; // Lock:this, Key(Special):1

    [Key(2)]
    private StorageId storageId0; // Lock:this, Key(Special):2

    [Key(3)]
    private StorageId storageId1; // Lock:this, Key(Special):3

    [Key(4)]
    private StorageId storageId2; // Lock:this, Key(Special):4

    private object? data; // Lock:this
    private uint state; // Lock:this
    private int size; // Lock:this

#pragma warning disable SA1401 // Fields should be private
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
    internal StorageObject? previous;
    internal StorageObject? next;
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
#pragma warning restore SA1401 // Fields should be private

    public IStructualRoot? StructualRoot { get; set; } // Lock:

    public IStructualObject? StructualParent { get; set; } // Lock:

    public int StructualKey { get; set; } // Lock:

    public ulong PointId => this.pointId;

    public uint TypeIdentifier => this.typeIdentifier;

    public int Size => this.size;

    internal StorageControl storageControl => this.storageMap.StorageControl;

    internal StorageMap storageMap => this.StructualRoot is ICrystal crystal ? crystal.Storage.StorageMap : StorageControl.Default.DisabledMap;

    public bool IsDisabled => (this.state & DisabledStateBit) != 0;

    public new bool IsLocked => (this.state & LockedStateBit) != 0;

    public bool IsRip => (this.state & RipStateBit) != 0;

    public bool IsPendingRelease => (this.state & PendingReleaseStateBit) != 0;

    // public bool IsPendingRip => (this.state & PendingRipStateBit) != 0;

    public bool CanUnload => !this.IsLocked;

    #endregion

    internal StorageObject()
    {
    }

    internal StorageObject(ulong pointId, uint typeIdentifier)
    {
        this.pointId = pointId;
        this.typeIdentifier = typeIdentifier;
    }

    internal StorageObject(uint typeIdentifier, bool disabledStorage)
    {
        this.typeIdentifier = typeIdentifier;
        if (disabledStorage)
        {// Disable storage
            this.state |= DisabledStateBit;
        }
    }

    internal void Initialize(ulong pointId, uint typeIdentifier, bool disabledStorage)
    {
        this.pointId = pointId;
        this.typeIdentifier = typeIdentifier;
        if (disabledStorage)
        {// Disable storage
            this.state |= DisabledStateBit;
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

    internal async ValueTask<TData?> TryGet<TData>(bool createIfNotExists = true)
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
                // this.PrepareData() is called from PrepareAndLoadInternal().
            }

            if (this.data is null && createIfNotExists)
            {// Reconstruct
                this.SetDataInternal(TinyhandSerializer.Reconstruct<TData>());
            }

            return (TData?)this.data;
        }
        finally
        {
            this.Exit();
        }
    }

    internal async ValueTask<TData?> TryLock<TData>(bool createIfNotExists = true)
    {
        await this.EnterAsync().ConfigureAwait(false);
        try
        {
            if (this.storageControl.IsRip)
            {
                return default;
            }

            if (this.IsRip || this.IsLocked)
            {
                return default;
            }

            if (this.data is null)
            {// PrepareAndLoad
                await this.PrepareAndLoadInternal<TData>().ConfigureAwait(false);
                // this.PrepareData() is called from PrepareAndLoadInternal().
            }

            if (this.data is null && createIfNotExists)
            {// Reconstruct
                this.SetDataInternal(TinyhandSerializer.Reconstruct<TData>());
            }

            if (!this.TryLockInternal())
            {
                return default;
            }

            return (TData?)this.data;
        }
        finally
        {
            this.Exit();
        }
    }

    internal async ValueTask Unlock()
    {
        await this.EnterAsync().ConfigureAwait(false);
        try
        {
            if (this.IsDisabled)
            {
                return;
            }

            if (!this.IsLocked)
            {
                return;
            }

            this.UnlockInternal();

            if (this.IsPendingRelease || this.storageControl.IsRip)
            {
            }
        }
        finally
        {
            this.Exit();
        }
    }

    internal void Set<TData>(TData data)
    {
        using (this.Lock())
        {
            this.SetDataInternal(data);
        }
    }

    /*internal bool Probe(ProbeMode probeMode)
    {
        if (probeMode == ProbeMode.IsUnloadableAll)
        {
            if (this.IsLocked)
            {// Locked (not unloadable)
                return false;
            }
        }
        else if (probeMode == ProbeMode.IsUnloadedAll)
        {
            if (!this.IsUnloaded && !this.IsDisabled)
            {// Not unloaded and not disabled
                return false;
            }
        }
        else if (probeMode == ProbeMode.LockAll)
        {
            this.TryLockInternal();
        }
        else if (probeMode == ProbeMode.UnlockAll)
        {
            this.UnlockInternal();
        }

        return false;
    }*/

    internal async Task<bool> StoreData(StoreMode storeMode)
    {
        if (this.data is null)
        {// No data
            return true;
        }

        var result = false;
        await this.EnterAsync().ConfigureAwait(false); // using (this.Lock())
        try
        {
            result = await this.StoreDataInternal(storeMode).ConfigureAwait(false);
        }
        finally
        {
            this.Exit();
        }

        return result;
    }

    internal void Erase()
    {
        this.EraseStorage();
        ((IStructualObject)this).AddJournalRecord(JournalRecord.EraseStorage);
    }

    #region IStructualObject

    void IStructualObject.SetupStructure(IStructualObject? parent, int key)
    {
        ((IStructualObject)this).SetParentAndKey(parent, key);

        /*if (this.data is IStructualObject structualObject)
        {
            structualObject.SetupStructure(parent, key);
        }*/
    }

    bool IStructualObject.ReadRecord(ref TinyhandReader reader)
    {
        if (!reader.TryPeek(out JournalRecord record))
        {
            return false;
        }

        if (record == JournalRecord.EraseStorage)
        {// Erase storage
            this.EraseStorage();
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
            this.AddInternal(crystal, storageId);
            return true;
        }

        if (this.data is null)
        {
            this.data = this.TryGet<object>(false).Result;
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

    internal async Task<bool> StoreDataInternal(StoreMode storeMode)
    {// Lock:this
        if (this.data is null)
        {// No data
            return true;
        }

        if (this.StructualRoot is not ICrystal crystal)
        {// No crystal
            return true;
        }

        // Save children
        if (this.data is IStructualObject structualObject)
        {
            var result = await structualObject.Save(UnloadMode.TryUnload).ConfigureAwait(false);
            if (!result)
            {
                return false;
            }
        }

        var currentPosition = crystal.Journal is null ? Waypoint.ValidJournalPosition : crystal.Journal.GetCurrentPosition();

        // Serialize and get hash.
        var rentMemory = TinyhandSerializer.SerializeToRentMemory(this.data);
        var dataSize = rentMemory.Span.Length;
        var hash = FarmHash.Hash64(rentMemory.Span);

        if (hash != this.storageId0.Hash)
        {// Different data
         // Put
            ulong fileId = 0;
            crystal.Storage.PutAndForget(ref fileId, rentMemory.ReadOnly);
            var storageId = new StorageId(currentPosition, fileId, hash);

            // Update histories
            this.AddInternal(crystal, storageId);

            // Journal
            AddJournal();
            void AddJournal()
            {
                if (((IStructualObject)this).TryGetJournalWriter(out var root, out var writer, true) == true)
                {
                    writer.Write(JournalRecord.AddStorage);
                    TinyhandSerializer.SerializeObject(ref writer, storageId);
                    root.AddJournal(ref writer);
                }
            }
        }

        rentMemory.Return();

        if (storeMode == StoreMode.Release)
        {// Release
            this.storageControl.Remove(this, false);
            this.data = default;
        }

        return true;
    }

    private async Task PrepareAndLoadInternal<TData>()
    {// using (this.Lock())
        if (this.data is not null)
        {
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
                this.SetDataInternal(data);
            }
        }
        finally
        {
            result.Return();
        }
    }

    internal void SetDataInternal<TData>(TData data)
    {// this.Lock() required
        BytePool.RentMemory rentMemory = default;

        this.data = data;
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

        if (((IStructualObject)this).TryGetJournalWriter(out var root, out var writer, true) == true)
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

    private void AddInternal(ICrystal crystal, StorageId storageId)
    {
        var numberOfHistories = crystal.CrystalConfiguration.NumberOfFileHistories;
        ulong fileId;
        var storage = crystal.Storage;

        if (numberOfHistories <= 1)
        {
            this.storageId0 = storageId;
        }
        else if (numberOfHistories == 2)
        {
            if (this.storageId1.IsValid)
            {
                fileId = this.storageId1.FileId;
                storage.DeleteAndForget(ref fileId);
            }

            this.storageId1 = this.storageId0;
            this.storageId0 = storageId;
        }
        else
        {
            if (this.storageId2.IsValid)
            {
                fileId = this.storageId2.FileId;
                storage.DeleteAndForget(ref fileId);
            }

            this.storageId2 = this.storageId1;
            this.storageId1 = this.storageId0;
            this.storageId0 = storageId;
        }

        /*else
        {
            if (this.storageId3.IsValid)
            {
                fileId = this.storageId3.FileId;
                storage.DeleteAndForget(ref fileId);
            }

            this.storageId3 = this.storageId2;
            this.storageId2 = this.storageId1;
            this.storageId1 = this.storageId0;
            this.storageId0 = storageId;
        }*/
    }

    private void EraseStorage()
    {
        IStructualObject? structualObject;
        ulong id0;
        ulong id1;
        ulong id2;
        // ulong id3;

        using (this.Lock())
        {
            structualObject = this.data as IStructualObject;

            id0 = this.storageId0.FileId;
            id1 = this.storageId1.FileId;
            id2 = this.storageId2.FileId;
            // id3 = this.storageId3.FileId;

            this.data = default;
            this.storageId0 = default;
            this.storageId1 = default;
            this.storageId2 = default;
            // this.storageId3 = default;
        }

        if (((IStructualObject)this).StructualRoot is ICrystal crystal)
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

        if (structualObject is not null)
        {
            structualObject.Erase();
        }
    }

    private bool TryLockInternal()
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
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ConfigureStorage(bool disableStorage)
    {
        this.Enter(); // using (this.Lock())

        if (disableStorage)
        {
            this.state |= DisabledStateBit;
        }
        else
        {// Enable storage
            if (this.storageMap.IsEnabled)
            {
                this.state &= ~DisabledStateBit;
            }
        }

        this.Exit();
    }
}
