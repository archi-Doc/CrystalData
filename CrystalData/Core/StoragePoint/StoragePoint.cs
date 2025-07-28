// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Tinyhand.IO;

namespace CrystalData;

#pragma warning disable SA1204 // Static elements should appear before instance elements

/// <summary>
/// <see cref="StoragePoint{TData}"/> is an independent component of the data tree, responsible for loading and persisting data.
/// </summary>
/// <typeparam name="TData">The type of data.</typeparam>
[TinyhandObject(ExplicitKeyOnly = true)]
public partial struct StoragePoint<TData> : ITinyhandSerializable<StoragePoint<TData>>, ITinyhandReconstructable<StoragePoint<TData>>, ITinyhandCloneable<StoragePoint<TData>>, IStructualObject
{
    #region FiendAndProperty

    public Type DataType
        => typeof(TData);

    private ulong pointId;

    private StoragePointClass<TData>? underlyingStoragePoint;

    private StoragePointClass<TData> UnderlyingStoragePoint
    {
        get
        {
            if (this.underlyingStoragePoint is not null)
            {
                return this.underlyingStoragePoint;
            }

            if (((IStructualObject)this).StructualRoot is ICrystal crystal)
            {
                this.underlyingStoragePoint = (StoragePointClass<TData>)crystal.Crystalizer.StorageControl.GetOrCreate(ref this.pointId);
                return this.underlyingStoragePoint;
            }

            this.underlyingStoragePoint = new();
            return this.underlyingStoragePoint;
        }
    }

    #endregion

    public StoragePoint()
    {
    }

    public StoragePoint(ulong pointId)
    {
        this.pointId = pointId;
    }

    public void Set(TData data) => this.UnderlyingStoragePoint.Set(data);

    #region IStructualObject

    IStructualRoot? IStructualObject.StructualRoot
    {
        get => this.underlyingStoragePoint?.StructualRoot;
        set
        {
            if (this.underlyingStoragePoint is not null)
            {
                this.underlyingStoragePoint.StructualRoot = value;
            }
        }
    }

    IStructualObject? IStructualObject.StructualParent
    {
        get => this.underlyingStoragePoint?.StructualParent;
        set
        {
            if (this.underlyingStoragePoint is not null)
            {
                this.underlyingStoragePoint.StructualParent = value;
            }
        }
    }

    int IStructualObject.StructualKey
    {
        get => this.underlyingStoragePoint is null ? 0 : this.underlyingStoragePoint.StructualKey;
        set
        {
            if (this.underlyingStoragePoint is not null)
            {
                this.underlyingStoragePoint.StructualKey = value;
            }
        }
    }

    void IStructualObject.SetupStructure(IStructualObject? parent, int key)
    {//
        ((IStructualObject)this).SetParentAndKey(parent, key);
    }

    #endregion

    static void ITinyhandSerializable<StoragePoint<TData>>.Serialize(ref TinyhandWriter writer, scoped ref StoragePoint<TData> v, TinyhandSerializerOptions options)
    {
        writer.Write(v.pointId);
    }

    static unsafe void ITinyhandSerializable<StoragePoint<TData>>.Deserialize(ref TinyhandReader reader, scoped ref StoragePoint<TData> v, TinyhandSerializerOptions options)
    {
        v.pointId = reader.ReadUInt64();
    }

    static unsafe void ITinyhandReconstructable<StoragePoint<TData>>.Reconstruct([NotNull] scoped ref StoragePoint<TData> v, TinyhandSerializerOptions options)
    {
        v = default;
    }

    static unsafe StoragePoint<TData> ITinyhandCloneable<StoragePoint<TData>>.Clone(scoped ref StoragePoint<TData> v, TinyhandSerializerOptions options)
    {
        return new(v.pointId);
    }
}

[TinyhandObject]
[ValueLinkObject(Isolation = IsolationLevel.Serializable)]
public partial class StoragePointClass<TData> : SemaphoreLock, IStructualObject, IStoragePoint, IStorageData, ITinyhandSerializable<StoragePointClass<TData>>, ITinyhandReconstructable<StoragePointClass<TData>>, ITinyhandCloneable<StoragePointClass<TData>>
{
    public const int MaxHistories = 3; // 4

    private const uint DisabledStateBit = 1u << 31;
    private const uint UnloadingStateBit = 1u << 30;
    private const uint UnloadedStateBit = 1u << 29;
    private const uint UnloadingAndUnloadedStateBit = UnloadingStateBit | UnloadedStateBit;
    private const uint LockedStateBit = 1u << 0;
    // private const uint StateMask = 0xFFFF0000;
    // private const uint NegativeStateMask = 0xFF000000;
    // private const uint LockCountMask = 0x0000FFFF;

    #region FieldAndProperty

    [Link(Primary = true, Unique = true, Type = ChainType.Unordered, AddValue = false)]
    public ulong PointId { get; private set; } // Lock:StorageControl, Key:0

    private TData? data; // Lock:this
    private uint typeIdentifier; // Lock:this, Key(Special):1
    private StorageId storageId0; // Lock:this, Key(Special):2
    private StorageId storageId1; // Lock:this, Key(Special):3
    private StorageId storageId2; // Lock:this, Key(Special):4

    public IStructualRoot? StructualRoot { get; set; } // Lock:

    public IStructualObject? StructualParent { get; set; } // Lock:

    public int StructualKey { get; set; } // Lock:

    private uint state; // Lock:this

    // Chain Lock:StorageControl

    /// <summary>
    /// Gets a value indicating whether storage is disabled, and data is serialized directly.
    /// </summary>
    public bool IsDisabled => (this.state & DisabledStateBit) != 0;

    public new bool IsLocked => (this.state & LockedStateBit) != 0;

    public bool IsUnloading => (this.state & UnloadingStateBit) != 0;

    public bool IsUnloaded => (this.state & UnloadedStateBit) != 0;

    public bool IsUnloadingOrUnloaded => (this.state & UnloadingAndUnloadedStateBit) != 0;

    public bool CanUnload => !this.IsLocked;

    // private uint LockCount => this.state & LockCountMask;

    #endregion

    #region Tinyhand

    static void ITinyhandSerializable<StoragePointClass<TData>>.Serialize(ref TinyhandWriter writer, scoped ref StoragePointClass<TData>? v, TinyhandSerializerOptions options)
    {
        if (v == null)
        {
            writer.WriteNil();
            return;
        }

        if (v.IsDisabled)
        {// Storage disabled
            TinyhandSerializer.Serialize(ref writer, v.data, options);
            return;
        }

        if (options.IsSpecialMode)
        {// StorageControl
            writer.WriteArrayHeader(5);

            writer.Write(v.PointId);
            writer.Write(v.typeIdentifier);
            TinyhandSerializer.SerializeObject(ref writer, v.storageId0, options);
            TinyhandSerializer.SerializeObject(ref writer, v.storageId1, options);
            TinyhandSerializer.SerializeObject(ref writer, v.storageId2, options);
        }
        else if (options.IsSignatureMode)
        {// Signature
            writer.Write(0x8bc0a639u);
            writer.Write(v.PointId);
        }
        else
        {// In-class
            writer.Write(v.PointId);
        }
    }

    static void ITinyhandSerializable<StoragePointClass<TData>>.Deserialize(ref TinyhandReader reader, scoped ref StoragePointClass<TData>? v, TinyhandSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return;
        }

        v ??= new CrystalData.StoragePointClass<TData>();
        if (!options.IsSpecialMode)
        {
            // If the type is interger, it is treated as PointId; otherwise, deserialization is attempted as TData (since TData is not expected to be of interger type, this should generally work without issue).
            if (reader.TryReadUInt64(out var pointId))
            {
                v.PointId = pointId;
            }
            else
            {
                v.data = TinyhandSerializer.Deserialize<TData>(ref reader, options);
            }

            return;
        }

        // StorageControl
        var numberOfData = reader.ReadArrayHeader();
        options.Security.DepthStep(ref reader);
        try
        {
            if (numberOfData-- > 0 && !reader.TryReadNil())
            {
                v.PointId = reader.ReadUInt64();
            }

            if (numberOfData-- > 0 && !reader.TryReadNil())
            {
                v.typeIdentifier = reader.ReadUInt32();
            }

            if (numberOfData-- > 0 && !reader.TryReadNil())
            {
                CrystalData.StorageId vd = v.storageId0!;
                TinyhandSerializer.DeserializeObject(ref reader, ref vd!, options);
                v.storageId0 = vd;
            }
            else
            {
                v.storageId0 = default;
            }

            if (numberOfData-- > 0 && !reader.TryReadNil())
            {
                CrystalData.StorageId vd = v.storageId1!;
                TinyhandSerializer.DeserializeObject(ref reader, ref vd!, options);
                v.storageId1 = vd;
            }
            else
            {
                v.storageId1 = default;
            }

            if (numberOfData-- > 0 && !reader.TryReadNil())
            {
                CrystalData.StorageId vd = v.storageId2!;
                TinyhandSerializer.DeserializeObject(ref reader, ref vd!, options);
                v.storageId2 = vd;
            }
            else
            {
                v.storageId2 = default;
            }

            while (numberOfData-- > 0)
            {
                reader.Skip();
            }
        }
        finally
        {
            reader.Depth--;
        }
    }

    static void ITinyhandReconstructable<StoragePointClass<TData>>.Reconstruct([NotNull] scoped ref StoragePointClass<TData>? v, TinyhandSerializerOptions options)
    {
        v ??= new CrystalData.StoragePointClass<TData>();
    }

    static StoragePointClass<TData>? ITinyhandCloneable<StoragePointClass<TData>>.Clone(scoped ref StoragePointClass<TData>? v, TinyhandSerializerOptions options)
    {
        if (v == null)
        {
            return null;
        }

        var value = new CrystalData.StoragePointClass<TData>();
        value.PointId = v.PointId;
        return value;
    }

    #endregion

    [Link(Type = ChainType.LinkedList, Name = "LastAccessed")]
    public StoragePointClass()
    {
    }

    public StoragePointClass(bool disableStorage = false)
    {
        this.ConfigureStorage(disableStorage);
    }

    public void Configure(bool disableStorage = false)
    {
        this.Enter(); // using (this.Lock())
        this.ConfigureStorage(disableStorage);
        this.Exit();
    }

    public async ValueTask<TData?> TryGet(bool createIfNotExists = true)
    {
        if (this.data is { } data)
        {
            return data;
        }

        await this.EnterAsync().ConfigureAwait(false);
        try
        {
            if (this.IsUnloadingOrUnloaded)
            {// Unloading or unloaded
                return default;
            }

            if (this.data is null)
            {// PrepareAndLoad
                await this.PrepareAndLoadInternal().ConfigureAwait(false);
                // this.PrepareData() is called from PrepareAndLoadInternal().
            }

            if (this.data is null && createIfNotExists)
            {// Reconstruct
                this.data = TinyhandSerializer.Reconstruct<TData>();
                this.PrepareData(0);
            }

            return this.data;
        }
        finally
        {
            this.Exit();
        }
    }

    public async ValueTask<TData?> TryLock(bool createIfNotExists = true)
    {
        await this.EnterAsync().ConfigureAwait(false);
        try
        {
            if (this.IsUnloaded)
            {
                return default;
            }

            if (!this.TryLockInternal())
            {
                return default;
            }

            if (this.data is null)
            {// PrepareAndLoad
                if (!this.IsUnloaded)
                {
                    await this.PrepareAndLoadInternal().ConfigureAwait(false);
                    // this.PrepareData() is called from PrepareAndLoadInternal().
                }
            }

            if (this.data is null)
            {// Reconstruct
                if (!this.IsUnloaded)
                {
                    this.data = TinyhandSerializer.Reconstruct<TData>();
                    this.PrepareData(0);
                }
            }

            return this.data;
        }
        finally
        {
            this.Exit();
        }
    }

    public void Set(TData data, int sizeHint = 0)
    {// Journaling is not supported.
        using (this.Lock())
        {
            this.data = data;
        }

        if (((IStructualObject)this).StructualRoot is ICrystal crystal)
        {
            crystal.Crystalizer.Memory.Register(this, sizeHint);
        }
    }

    public Type DataType
        => typeof(TData);

    public async Task<bool> Save(UnloadMode2 mode)
    {
        return false;
    }

    public bool Probe(ProbeMode probeMode)
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
    }

    public async Task<bool> Save(UnloadMode unloadMode)
    {
        if (this.data is null)
        {// No data
            return true;
        }

        await this.EnterAsync().ConfigureAwait(false); // using (this.Lock())
        try
        {
            if (this.data is null)
            {// No data
                return true;
            }

            if (((IStructualObject)this).StructualRoot is not ICrystal crystal)
            {// No crystal
                return true;
            }

            // Save children
            if (this.data is IStructualObject structualObject)
            {
                var result = await structualObject.Save(unloadMode).ConfigureAwait(false);
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
                        if (this is ITinyhandCustomJournal tinyhandCustomJournal)
                        {
                            tinyhandCustomJournal.WriteCustomLocator(ref writer);
                        }

                        writer.Write(JournalRecord.AddStorage);
                        TinyhandSerializer.SerializeObject(ref writer, storageId);
                        root.AddJournal(ref writer);
                    }
                }
            }

            rentMemory.Return();

            if (unloadMode.IsUnload())
            {// Unload
                crystal.Crystalizer.Memory.ReportUnloaded(this, dataSize);
                this.data = default;
            }
        }
        finally
        {
            this.Exit();
        }

        return true;
    }

    public void Erase()
    {
        this.EraseStorage();
        ((IStructualObject)this).AddJournalRecord(JournalRecord.EraseStorage);
    }

    public bool DataEquals(StoragePointClass<TData>? other)
    {
        if (other is null)
        {
            return false;
        }

        var data = this.TryGet().Result;
        var otherData = other.TryGet().Result;
        if (data is null)
        {
            return otherData is null;
        }
        else
        {
            return data.Equals(otherData);
        }
    }

    public bool DataEquals(TData? otherData)
    {
        var data = this.TryGet().Result;
        if (data is null)
        {
            return otherData is null;
        }
        else
        {
            return data.Equals(otherData);
        }
    }

    #region IStructualObject

    void IStructualObject.SetupStructure(IStructualObject? parent, int key)
    {
        ((IStructualObject)this).SetParentAndKey(parent, key);
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
            this.data = this.TryGet(false).Result;
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

    private async Task PrepareAndLoadInternal()
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
            this.data = TinyhandSerializer.Deserialize<TData>(result.Data.Span);
            this.PrepareData(result.Data.Span.Length);
        }
        catch
        {
        }
        finally
        {
            result.Return();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PrepareData(int dataSize)
    {
        if (this.data is IStructualObject structualObject)
        {
            structualObject.SetupStructure(this);
        }

        if (((IStructualObject)this).StructualRoot is ICrystal crystal)
        {
            crystal.Crystalizer.Memory.Register(this, dataSize);
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
    private void ConfigureStorage(bool disableStorage)
    {
        if (disableStorage)
        {
            this.state |= DisabledStateBit;
        }
        else
        {
            this.state &= ~DisabledStateBit;
        }
    }
}
