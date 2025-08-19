// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using CrystalData.Internal;
using Tinyhand.IO;

namespace CrystalData;

#pragma warning disable SA1204 // Static elements should appear before instance elements
#pragma warning disable SA1401 // Fields should be private

/// <summary>
/// <see cref="StoragePoint{TData}"/> is an independent component of the data tree, responsible for loading and persisting data.<br/>
/// Thread-safe; however, please note that the thread safety of the data <see cref="StoragePoint{TData}"/> holds depends on the implementation of that data.
/// </summary>
/// <typeparam name="TData">The type of data.</typeparam>
[TinyhandObject(ExplicitKeyOnly = true)]
public partial class StoragePoint<TData> : ITinyhandSerializable<StoragePoint<TData>>, ITinyhandReconstructable<StoragePoint<TData>>, ITinyhandCloneable<StoragePoint<TData>>, ILockableData<TData>, IStructualObject
    where TData : notnull
{
    #region FiendAndProperty

    [Key(0)]
    protected ulong pointId; // Lock:StorageControl

    private StorageObject? storageObject; // Lock:StorageControl

    public ulong PointId => this.pointId;

    /// <summary>
    /// Gets the <see langword="uint"/> type identifier used by TinyhandSerializer.
    /// </summary>
    public uint TypeIdentifier => TinyhandTypeIdentifier.GetTypeIdentifier<TData>();

    /// <summary>
    /// Gets a value indicating whether storage is disabled, and data is serialized directly.
    /// </summary>
    public bool IsDisabled => this.GetOrCreateStorageObject().IsDisabled;

    /// <summary>
    /// Gets a value indicating whether storage is locked.<br/>
    /// Reading is possible, but writing or unloading is not allowed.
    /// </summary>
    public bool IsLocked => this.storageObject?.IsLocked == true;

    /// <summary>
    /// Gets a value indicating whether storage is rip.<br/>
    /// Storage is shutting down and is read-only.
    /// </summary>
    public bool IsRip => this.storageObject?.IsRip == true;

    /*/// <summary>
    /// Gets a value indicating whether storage is pending release.<br/>
    /// Once the lock is released, the storage will be persisted and memory will be freed.
    /// </summary>
    public bool IsPendingRelease => this.storageObject?.IsPendingRelease == true;*/

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="StoragePoint{TData}"/> class.
    /// </summary>
    public StoragePoint()
    {
    }

    /// <summary>
    /// Disables storage for this storage point, causing data to be serialized directly.
    /// </summary>
    public void DisableStorage()
        => this.GetOrCreateStorageObject().ConfigureStorage(true);

    /// <summary>
    /// Enables storage for this storage point, allowing data to be persisted to storage.
    /// </summary>
    public void EnableStorage()
        => this.GetOrCreateStorageObject().ConfigureStorage(false);

    /// <summary>
    /// Sets the data instance for this storage point.<br/>
    /// This function is not recommended, as instance replacement may cause data inconsistencies.
    /// </summary>
    /// <param name="data">The data to set.</param>]
    public void Set(TData data)
        => this.GetOrCreateStorageObject().Set(data);

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
        => this.GetOrCreateStorageObject().TryGet<TData>(Timeout.InfiniteTimeSpan, default);

    /// <summary>
    /// Asynchronously gets the data associated with this storage point, or creates it if it does not exist.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask{TData}"/> representing the asynchronous operation. The result contains the data.
    /// </returns>
    public ValueTask<TData> GetOrCreate()
        => this.GetOrCreateStorageObject().GetOrCreate<TData>();

    /// <summary>
    /// Attempts to acquire a lock on the storage object and returns the data if successful.<br/>
    /// If storage is shutting down, return <c>null</c>.<br/>
    /// Since data may be saved and released during storage operations, always lock the data when making changes.<br/>
    /// This is for storage operation locks only. Please use a different mechanism for object-level locks.<br/>
    /// To prevent deadlocks, always maintain a consistent lock order and never forget to unlock.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for the lock. If <see cref="TimeSpan.Zero"/>, the method returns immediately.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while waiting to acquire the lock.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask{TData}"/> representing the asynchronous operation. The result contains the data if the lock was acquired; otherwise, <c>null</c>.
    /// </returns>
    public ValueTask<DataScope<TData>> TryLock(TimeSpan timeout, CancellationToken cancellationToken) => this.GetOrCreateStorageObject().TryLock<TData>(timeout, cancellationToken);

    public ValueTask<DataScope<TData>> TryLock() => this.GetOrCreateStorageObject().TryLock<TData>(Timeout.InfiniteTimeSpan, default);

    /// <summary>
    /// Releases the lock previously acquired by <see cref="TryLock()"/>.<br/>
    /// To prevent deadlocks, always maintain a consistent lock order and never forget to unlock.
    /// </summary>
    public void Unlock() => this.GetOrCreateStorageObject().Unlock();

    // public ValueTask<DataScope<TData>> EnterScope() => this.GetOrCreateStorageObject().EnterScope<TData>();

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
    public void Delete()
        => this.GetOrCreateStorageObject().Delete();

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

    bool IStructualObject.ReadRecord(ref TinyhandReader reader)
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

        var previousPointId = this.pointId;
        storageMap.StorageControl.GetOrCreate<TData>(ref this.pointId, ref this.storageObject, storageMap);
        if (this.pointId != previousPointId &&
            ((IStructualObject)this).TryGetJournalWriter(out var root, out var writer, true) == true)
        {
            writer.Write(JournalRecord.Value);
            writer.Write(this.pointId);
            root.AddJournal(ref writer);
        }

        return this.storageObject;
    }
}
