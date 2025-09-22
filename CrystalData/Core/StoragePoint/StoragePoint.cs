// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using CrystalData.Internal;
using Tinyhand.IO;

namespace CrystalData;

#pragma warning disable SA1204 // Static elements should appear before instance elements
#pragma warning disable SA1401 // Fields should be private

/// <summary>
/// <see cref="StoragePoint{TData}"/> is an independent component of the data tree, responsible for loading and persisting data.<br/>
/// Thread-safe; however, please note that the thread safety of the data <see cref="StoragePoint{TData}"/> holds depends on the implementation of that data.<br/>
/// The <b>TinyhandObject.Key(0)</b> is reserved for <c>PointId</c>.
/// Use keys starting from <b>1 or greater</b> instead.
/// </summary>
/// <typeparam name="TData">The type of data.</typeparam>
[TinyhandObject(ExplicitKeyOnly = true, ReservedKeyCount = 1)]
public partial class StoragePoint<TData> : ITinyhandSerializable<StoragePoint<TData>>, ITinyhandReconstructable<StoragePoint<TData>>, ITinyhandCloneable<StoragePoint<TData>>, IStructualObject, IDataLocker<TData>
    where TData : class
{// object:16, ulong:8, StorageObject:8, Structual: 20
    #region FiendAndProperty

    [Key(0)]
    protected ulong pointId; // Lock:StorageControl

    private StorageObject? storageObject; // Lock:StorageControl

    public ulong PointId => this.pointId;

    ref ObjectProtectionState IDataLocker<TData>.GetProtectionStateRef() => ref this.GetOrCreateStorageObject().protectionState;

    /// <summary>
    /// Gets the <see langword="uint"/> type identifier used by TinyhandSerializer.
    /// </summary>
    public uint TypeIdentifier => TinyhandTypeIdentifier.GetTypeIdentifier<TData>();

    /// <summary>
    /// Gets a value indicating whether storage is enabled.
    /// </summary>
    public bool IsEnabled => this.GetOrCreateStorageObject().IsEnabled;

    /// <summary>
    /// Gets a value indicating whether storage is locked.<br/>
    /// Reading is possible, but writing or unloading is not allowed.
    /// </summary>
    public bool IsLocked => this.storageObject?.IsLocked == true;

    public bool IsDeleted => this.storageObject?.IsDeleted == true;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="StoragePoint{TData}"/> class.
    /// </summary>
    public StoragePoint()
    {
    }

    /// <summary>
    /// Sets the data instance for this storage point.<br/>
    /// This function is not recommended, as instance replacement may cause data inconsistencies.
    /// </summary>
    /// <param name="data">The data to set.</param>]
    public void Set(TData data)
        => this.GetOrCreateStorageObject().Set(data);

    #region IDataLocker

    /// <summary>
    /// Asynchronously gets the data associated with this storage point.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for the acquisition. If <see cref="TimeSpan.Zero"/>, the method returns immediately.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while waiting to acquire the data.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask{TData}"/> representing the asynchronous operation. The result contains the data if available; otherwise, <c>null</c>.
    /// </returns>
    public ValueTask<TData?> TryGet(TimeSpan timeout, CancellationToken cancellationToken)
        => this.GetOrCreateStorageObject().TryGet<TData>(timeout, cancellationToken);

    public ValueTask<TData?> TryGet()
        => this.GetOrCreateStorageObject().TryGet<TData>(ValueLinkGlobal.LockTimeout, default);

    /*/// <summary>
    /// Asynchronously gets the data associated with this storage point, or creates it if it does not exist.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask{TData}"/> representing the asynchronous operation. The result contains the data.
    /// </returns>
    public ValueTask<TData> GetOrCreate()
        => this.GetOrCreateStorageObject().GetOrCreate<TData>();*/

    /// <summary>
    /// Attempts to acquire a lock on the storage object and returns the data if successful.<br/>
    /// If storage is deleted or shutting down, return <c>null</c>.<br/>
    /// Since data may be saved and released during storage operations, always lock the data when making changes.<br/>
    /// This TryLock/Unlock mechanism provides exclusive control over both the storage lifecycle (loading and deletion) and the data itself.<br/>
    /// <b>To prevent deadlocks, always maintain a consistent lock order and never forget to unlock.</b>
    /// </summary>
    /// <param name="acquisitionMode">The data acquisition mode specifying get, create, or get-or-create behavior.</param>
    /// <param name="timeout">The maximum time to wait for the lock. If <see cref="TimeSpan.Zero"/>, the method returns immediately.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while waiting to acquire the lock.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask{TData}"/> representing the asynchronous operation. The result contains the data if the lock was acquired; otherwise, <c>null</c>.
    /// </returns>
    public ValueTask<DataScope<TData>> TryLock(AcquisitionMode acquisitionMode, TimeSpan timeout, CancellationToken cancellationToken = default)
        => this.GetOrCreateStorageObject().TryLock<TData>(acquisitionMode, timeout, cancellationToken);

    public ValueTask<DataScope<TData>> TryLock(AcquisitionMode acquisitionMode = AcquisitionMode.GetOrCreate)
        => this.GetOrCreateStorageObject().TryLock<TData>(acquisitionMode, ValueLinkGlobal.LockTimeout, default);

    ValueTask<DataScope<TData>> IDataLocker<TData>.TryLock(TimeSpan timeout, CancellationToken cancellationToken)
        => this.GetOrCreateStorageObject().TryLock<TData>(AcquisitionMode.GetOrCreate, timeout, cancellationToken);

    /// <summary>
    /// Releases the lock previously acquired by <see cref="TryLock(AcquisitionMode)"/>.<br/>
    /// To prevent deadlocks, always maintain a consistent lock order and never forget to unlock.
    /// </summary>
    public void Unlock() => this.GetOrCreateStorageObject().Unlock();

    /// <summary>
    /// Pins the data associated with this storage point in memory.<br/>
    /// This operation ensures the data remains in memory and is not released.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask{TData}"/> representing the asynchronous operation.<br/>
    /// The result contains the pinned data.
    /// </returns>
    public ValueTask<TData> PinData()
        => this.GetOrCreateStorageObject().PinData<TData>();

    /// <summary>
    /// Adds this storage point to the save queue.<br/>
    /// The save queue is used to schedule data persistence operations.
    /// </summary>
    /// <param name="delaySeconds">
    /// The number of seconds to delay before saving.<br/>
    /// If 0 is specified, the default delay time is used.
    /// </param>
    public void AddToSaveQueue(int delaySeconds = 0)
        => ((IStructualRoot)this.GetOrCreateStorageObject()).AddToSaveQueue(delaySeconds);

    #endregion

    public bool DataEquals(StoragePoint<TData> other)
    {
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

    public void DeleteLatestStorageForTest()
        => this.GetOrCreateStorageObject().DeleteLatestStorageForTest();

    #region IStructualObject

    IStructualRoot? IStructualObject.StructualRoot { get; set; }

    IStructualObject? IStructualObject.StructualParent { get; set; }

    int IStructualObject.StructualKey { get; set; }

    public Task<bool> StoreData(StoreMode storeMode)
    {
        if (this.storageObject is { } storageObject)
        {
            return storageObject.StoreData(storeMode);
        }
        else
        {
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Deletes the data associated with this storage point.<br/>
    /// This operation removes the data from storage and memory.
    /// </summary>
    /// <param name="forceDeleteAfter">The UTC <see cref="DateTime"/> after which the object will be forcibly deleted if not already deleted.<br/>
    /// <see langword="default"/>: Do not forcibly delete; wait until all operations are finished.<br/>
    /// <see cref="DateTime.UtcNow"/> or earlier: forcibly delete data without waiting.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous delete operation.
    /// </returns>
    public virtual Task DeleteData(DateTime forceDeleteAfter = default)
        => this.GetOrCreateStorageObject().DeleteData(forceDeleteAfter);

    /*void IStructualObject.SetupStructure(IStructualObject? parent, int key)
    {
        ((IStructualObject)this).StructualRoot = parent?.StructualRoot;

        if (this.storageObject is not null)
        {
            if (parent?.StructualRoot is ICrystal crystal)
            {
                StorageControl.Default.GetOrCreate<TData>(ref this.pointId, ref this.storageObject, crystal.Storage.StorageMap);
            }

            ((IStructualObject)this.storageObject).SetupStructure(parent, key);
        }
    }*/

    bool IStructualObject.ProcessJournalRecord(ref TinyhandReader reader)
    {
        if (reader.TryReadJournalRecord(out JournalRecord record))
        {
            if (record == JournalRecord.Value)
            {
                this.pointId = reader.ReadUInt64();
                return true;
            }
        }

        return false;
    }

    #endregion

    Task IDataLocker<TData>.DeletePoint(DateTime forceDeleteAfter)
        => this.GetOrCreateStorageObject().DeleteData(forceDeleteAfter);

    #region Tinyhand

    static void ITinyhandSerializable<StoragePoint<TData>>.Serialize(ref TinyhandWriter writer, scoped ref StoragePoint<TData>? v, TinyhandSerializerOptions options)
    {
        if (v is null)
        {
            writer.WriteNil();
        }
        else if (v.storageObject is null)
        {// In-class
            writer.Write(v.pointId);
        }
        else
        {// StorageObject (In-class or Storage disabled)
            v.storageObject.SerializeStoragePoint(ref writer, options);
        }
    }

    static unsafe void ITinyhandSerializable<StoragePoint<TData>>.Deserialize(ref TinyhandReader reader, scoped ref StoragePoint<TData>? v, TinyhandSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            v = default;
            return;
        }

        v ??= new();
        if (reader.TryReadUInt64(out var pointId))
        {
            // If the type is interger, it is treated as PointId; otherwise, deserialization is attempted as TData (since TData is not expected to be of interger type, this should generally work without issue).
            v.pointId = pointId;
        }
        else
        {
            StorageMap.Disabled.StorageControl.GetOrCreate<TData>(ref v.pointId, ref v.storageObject, StorageMap.Disabled);
            v.storageObject.SetTypeIdentifier<TData>(); // If the TypeIdentifier is changed, serialization becomes impossible, so update it.

            var data = TinyhandSerializer.Deserialize<TData>(ref reader, options) ?? TinyhandSerializer.Reconstruct<TData>(options);
            v.storageObject.Set(data);
        }
    }

    static unsafe void ITinyhandReconstructable<StoragePoint<TData>>.Reconstruct([NotNull] scoped ref StoragePoint<TData>? v, TinyhandSerializerOptions options)
    {
        v ??= new();
    }

    static unsafe StoragePoint<TData>? ITinyhandCloneable<StoragePoint<TData>>.Clone(scoped ref StoragePoint<TData>? v, TinyhandSerializerOptions options)
    {
        if (v is null)
        {
            return null;
        }

        var obj = new StoragePoint<TData>();
        obj.pointId = v.pointId;
        return obj;
    }

    #endregion

    private StorageObject GetOrCreateStorageObject()
    {
        if (this.storageObject is not null)
        {
            return this.storageObject;
        }

        var storageMap = StorageMap.Disabled;
        if (((IStructualObject)this).StructualRoot is ICrystal crystal)
        {
            storageMap = crystal.Storage.StorageMap;
        }
        else if (((IStructualObject)this).StructualRoot is StorageObject storageObject)
        {
            storageMap = storageObject.storageMap;
        }

        var previousPointId = this.pointId;
        storageMap.StorageControl.GetOrCreate<TData>(ref this.pointId, ref this.storageObject, storageMap);
        this.storageObject.SetTypeIdentifier<TData>(); // If the TypeIdentifier is changed, serialization becomes impossible, so update it.

        if (this.pointId != previousPointId &&
            ((IStructualObject)this).TryGetJournalWriter(out var root, out var writer, true) == true)
        {
            writer.Write(JournalRecord.Value);
            writer.Write(this.pointId);
            root.AddJournalAndDispose(ref writer);
        }

        return this.storageObject;
    }
}
