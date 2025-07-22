// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Tinyhand.IO;

namespace CrystalData;

/// <summary>
/// <see cref="StoragePoint{TData}"/> is an independent component of the data tree, responsible for loading and persisting partial data.
/// </summary>
/// <typeparam name="TData">The type of data.</typeparam>
[TinyhandObject]
[ValueLinkObject(Isolation = IsolationLevel.Serializable)]
public sealed partial class StoragePoint<TData> : SemaphoreLock, IStructualObject, IStoragePoint, IStorageData, ITinyhandSerializable<StoragePoint<TData>>, ITinyhandReconstructable<StoragePoint<TData>>, ITinyhandCloneable<StoragePoint<TData>>
{
    public const int MaxHistories = 3; // 4

    private const uint InvalidBit = 1u << 31;
    private const uint UnloadingBit = 1u << 30;
    private const uint UnloadedBit = 1u << 29;
    private const uint UnloadingAndUnloadedBit = UnloadingBit | UnloadedBit;
    private const uint StateMask = 0xFFFF0000;
    private const uint NegativeStateMask = 0xFF000000;
    private const uint LockCountMask = 0x0000FFFF;

    #region FieldAndProperty

    private TData? data; // SemaphoreLock
    private ulong pointId;
    private StorageId storageId0;
    private StorageId storageId1;
    private StorageId storageId2;
    // private StorageId storageId3;

    [IgnoreMember]
    IStructualRoot? IStructualObject.StructualRoot { get; set; }

    [IgnoreMember]
    IStructualObject? IStructualObject.StructualParent { get; set; }

    [IgnoreMember]
    int IStructualObject.StructualKey { get; set; }

    // 31bit:Invalid storage, 30bit:Unloading, 29bit:Unload, 23-0bit:Lock count.
    private uint state; // SemaphoreLock

    public bool IsActive => (this.state & NegativeStateMask) == 0;

    public bool IsInvalid => (this.state & InvalidBit) != 0;

    public bool IsLocked => (this.state & LockCountMask) != 0;

    public bool IsUnloading => (this.state & UnloadingBit) != 0;

    public bool IsUnloaded => (this.state & UnloadedBit) != 0;

    public bool IsUnloadingOrUnloaded => (this.state & UnloadingAndUnloadedBit) != 0;

    public bool CanUnload => this.LockCount == 0;

    private uint LockCount => this.state & LockCountMask;

    #endregion

    #region Tinyhand

    static void ITinyhandSerializable<StoragePoint<TData>>.Serialize(ref TinyhandWriter writer, scoped ref StoragePoint<TData>? v, TinyhandSerializerOptions options)
    {
        if (v == null)
        {
            writer.WriteNil();
        }
        else
        {
            writer.Write(v.pointId);
        }
    }

    static void ITinyhandSerializable<StoragePoint<TData>>.Deserialize(ref TinyhandReader reader, scoped ref StoragePoint<TData>? v, TinyhandSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return;
        }

        v ??= new CrystalData.StoragePoint<TData>();
        v.pointId = reader.ReadUInt64();
    }

    static void ITinyhandReconstructable<StoragePoint<TData>>.Reconstruct([NotNull] scoped ref StoragePoint<TData>? v, TinyhandSerializerOptions options)
    {
        v ??= new CrystalData.StoragePoint<TData>();
    }

    static StoragePoint<TData>? ITinyhandCloneable<StoragePoint<TData>>.Clone(scoped ref StoragePoint<TData>? v, TinyhandSerializerOptions options)
    {
        if (v == null)
        {
            return null;
        }
        else
        {
            var value = new CrystalData.StoragePoint<TData>();
            value.pointId = v.pointId;
            return value;
        }
    }

    #endregion

    public StoragePoint()
    {
    }

    public StoragePoint(bool invalidStorage = false)
    {
        if (invalidStorage)
        {
            this.state |= InvalidBit;
        }
    }

    /*public void InvalidateStorage(bool invalidStorage)
    {
        using (this.Lock())
        {
            if (invalidStorage)
            {
                this.state |= InvalidBit;
            }
            else
            {
                this.state &= ~InvalidBit;
            }
        }
    }*/

    public async ValueTask<TData?> TryGet()
    {
        if (this.data is { } data)
        {
            return data;
        }

        await this.EnterAsync().ConfigureAwait(false);
        try
        {
            if (this.IsUnloadingOrUnloaded)
            {
                return default;
            }

            if (this.data is null)
            {// PrepareAndLoad
                await this.PrepareAndLoadInternal().ConfigureAwait(false);
                // this.PrepareData() is called from PrepareAndLoadInternal().
            }

            if (this.data is null)
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

    public async ValueTask<TData?> TryLock()
    {
        await this.EnterAsync().ConfigureAwait(false);
        try
        {
            if (this.IsUnloaded)
            {
                return default;
            }

            if (this.LockCount == 0)
            {
                this.state = (this.state & 0xFF000000) | 1;
            }
            else
            {
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
            if (!this.IsUnloaded && !this.IsInvalid)
            {// Not unloaded and not invalid
                return false;
            }
        }
        else if (probeMode == ProbeMode.LockAll)
        {
            this.IncrementLockCountInternal();
        }
        else if (probeMode == ProbeMode.UnlockAll)
        {
            this.DecrementLockCountInternal();
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
        this.EraseInternal();
        ((IStructualObject)this).AddJournalRecord(JournalRecord.EraseStorage);
    }

    #region Journal

    bool IStructualObject.ReadRecord(ref TinyhandReader reader)
    {
        if (!reader.TryPeek(out JournalRecord record))
        {
            return false;
        }

        if (record == JournalRecord.EraseStorage)
        {// Erase storage
            this.EraseInternal();
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
            this.data = this.TryGet().Result;
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
            structualObject.SetParent(this);
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

    private void EraseInternal()
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

    private void IncrementLockCountInternal()
    {
        this.state = (this.state & StateMask) | (this.LockCount + 1);
    }

    private void DecrementLockCountInternal()
    {
        this.state = (this.state & StateMask) | (this.LockCount - 1);
    }
}
