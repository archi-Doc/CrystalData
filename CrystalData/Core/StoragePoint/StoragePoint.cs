// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using CrystalData.Internal;
using Tinyhand.IO;

namespace CrystalData;

#pragma warning disable SA1204 // Static elements should appear before instance elements

/// <summary>
/// <see cref="StoragePoint{TData}"/> is an independent component of the data tree, responsible for loading and persisting data.<br/>
/// Thread-safe; however, please note that the thread safety of the data <see cref="StoragePoint{TData}"/> holds depends on the implementation of that data.
/// </summary>
/// <typeparam name="TData">The type of data.</typeparam>
[TinyhandObject(ExplicitKeyOnly = true)]
public partial class StoragePoint<TData> : ITinyhandSerializable<StoragePoint<TData>>, ITinyhandReconstructable<StoragePoint<TData>>, ITinyhandCloneable<StoragePoint<TData>>, IStoragePoint, IStructualObject
    where TData : notnull
{
    #region FiendAndProperty

    private ulong pointId; // Lock:StorageControl
    private StorageObject? storageObject; // Lock:StorageControl

    public uint TypeIdentifier => TinyhandTypeIdentifier.GetTypeIdentifier<TData>();

    /// <summary>
    /// Gets a value indicating whether storage is disabled, and data is serialized directly.
    /// </summary>
    public bool IsDisabled => this.GetStorageObject().IsDisabled;

    /// <summary>
    /// Gets a value indicating whether storage is locked.<br/>
    /// Reading is possible, but writing or unloading is not allowed.
    /// </summary>
    public bool IsLocked => this.GetStorageObject().IsLocked;

    /// <summary>
    /// Gets a value indicating whether storage is rip.<br/>
    /// Storage is shutting down and is read-only.
    /// </summary>
    public bool IsRip => this.GetStorageObject().IsRip;

    /// <summary>
    /// Gets a value indicating whether storage is pending release.<br/>
    /// Once the lock is released, the storage will be persisted and memory will be freed.
    /// </summary>
    public bool IsPendingRelease => this.GetStorageObject().IsPendingRelease;

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
        => this.GetStorageObject().ConfigureStorage(true);

    /// <summary>
    /// Enables storage for this storage point, allowing data to be persisted to storage.
    /// </summary>
    public void EnableStorage()
        => this.GetStorageObject().ConfigureStorage(false);

    /// <summary>
    /// Sets the data instance for this storage point.<br/>
    /// This function is not recommended, as instance replacement may cause data inconsistencies.
    /// </summary>
    /// <param name="data">The data to set.</param>
    public void Set(TData data)
        => this.GetStorageObject().Set(data);

    /// <summary>
    /// Asynchronously gets the data associated with this storage point.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask{TData}"/> representing the asynchronous operation. The result contains the data if available; otherwise, <c>null</c>.
    /// </returns>
    public ValueTask<TData?> Get()
        => this.GetStorageObject().Get<TData>();

    /// <summary>
    /// Asynchronously gets the data associated with this storage point, or creates it if it does not exist.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask{TData}"/> representing the asynchronous operation. The result contains the data.
    /// </returns>
    public ValueTask<TData> GetOrCreate()
        => this.GetStorageObject().GetOrCreate<TData>();

    // public ValueTask<TData?> TryLock() => this.GetStorageObject().TryLock<TData>();

    // public void Unlock() => this.GetStorageObject().Unlock();

    public bool DataEquals(StoragePoint<TData> other)
    {
        var data = this.Get().Result;
        var otherData = other.Get().Result;
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
        var data = this.Get().Result;
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
        {// StorageObject
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
            StorageControl.Default.GetOrCreate<TData>(ref v.pointId, ref v.storageObject, StorageControl.Default.DisabledMap);
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

    Task<bool> IStoragePoint.StoreData(StoreMode storeMode)
        => this.GetStorageObject().StoreData(storeMode);

    private StorageObject GetStorageObject()
    {
        if (this.storageObject is not null)
        {
            return this.storageObject;
        }

        StorageMap storageMap = StorageControl.Default.DisabledMap;
        if (((IStructualObject)this).StructualRoot is ICrystal crystal)
        {
            storageMap = crystal.Storage.StorageMap;
        }

        StorageControl.Default.GetOrCreate<TData>(ref this.pointId, ref this.storageObject, storageMap);
        return this.storageObject;
    }
}
