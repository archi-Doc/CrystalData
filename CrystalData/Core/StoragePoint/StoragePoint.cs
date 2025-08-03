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
public partial class StoragePoint<TData> : ITinyhandSerializable<StoragePoint<TData>>, ITinyhandReconstructable<StoragePoint<TData>>, ITinyhandCloneable<StoragePoint<TData>>, IStructualObject, IStoragePoint
{
    #region FiendAndProperty

    private ulong pointId; // Lock:StorageControl
    private StorageObject? storageObject; // Lock:StorageControl

    public Type DataType
        => typeof(TData);

    public uint TypeIdentifier
        => TinyhandTypeIdentifier.GetTypeIdentifier<TData>();

    /// <summary>
    /// Gets a value indicating whether storage is disabled, and data is serialized directly.
    /// </summary>
    public bool IsDisabled => this.GetOrCreate().IsDisabled;

    /// <summary>
    /// Gets a value indicating whether storage is locked.<br/>
    /// Reading is possible, but writing or unloading is not allowed.
    /// </summary>
    public bool IsLocked => this.GetOrCreate().IsLocked;

    public bool IsRip => this.GetOrCreate().IsRip;

    public bool IsPendingRip => this.GetOrCreate().IsPendingRip;

    public bool IsPendingRelease => this.GetOrCreate().IsPendingRelease;

    #endregion

    public StoragePoint()
    {
    }

    public void DisableStorage()
        => this.GetOrCreate().ConfigureStorage(true);

    public void EnableStorage()
        => this.GetOrCreate().ConfigureStorage(false);

    public void Set(TData data)
        => this.GetOrCreate().Set(data);

    public ValueTask<TData?> TryGet()
        => this.GetOrCreate().TryGet<TData>();

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

    IStructualRoot? IStructualObject.StructualRoot
    {// Delegate properties to the underlying StorageObject.
        get => this.storageObject?.StructualRoot;
        set
        {
            if (this.storageObject is not null)
            {
                this.storageObject.StructualRoot = value;
            }
        }
    }

    IStructualObject? IStructualObject.StructualParent
    {// Delegate properties to the underlying StorageObject.
        get => this.storageObject?.StructualParent;
        set
        {
            if (this.storageObject is not null)
            {
                this.storageObject.StructualParent = value;
            }
        }
    }

    int IStructualObject.StructualKey
    {// Delegate properties to the underlying StorageObject.
        get => this.storageObject is null ? 0 : this.storageObject.StructualKey;
        set
        {
            if (this.storageObject is not null)
            {
                this.storageObject.StructualKey = value;
            }
        }
    }

    void IStructualObject.SetupStructure(IStructualObject? parent, int key)
    {
        if (this.storageObject is not null)
        {
            ((IStructualObject)this.storageObject).SetupStructure(parent, key);
        }

        /*if (this.storageObject is null &&
            parent?.StructualRoot is ICrystal crystal)
        {
            crystal.Crystalizer.StorageControl.GetOrCreate<TData>(ref this.pointId, ref this.storageObject);
            ((IStructualObject)this.storageObject).SetParentAndKey(parent, key);
        }*/
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
            if (v.storageObject is not null)
            {
                StorageControl.Default.TryRemove(v.storageObject);
                v.storageObject = default;
            }

            var data = TinyhandSerializer.Deserialize<TData>(ref reader, options);

            StorageControl.Default.DisabledMap.GetOrCreate<TData>(ref v.pointId, ref v.storageObject);
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

    Task<bool> IStoragePoint.Save(UnloadMode2 unloadMode)
    {
        throw new NotImplementedException();
    }

    bool IStoragePoint.Probe(ProbeMode probeMode)
    {
        throw new NotImplementedException();
    }

    private StorageObject GetOrCreate()
    {
        if (this.storageObject is not null)
        {
            return this.storageObject;
        }

        StorageMap? storageMap = default;
        if (((IStructualObject)this).StructualRoot is ICrystal crystal)
        {
            storageMap = crystal.Storage.StorageMap;
        }

        storageMap ??= StorageControl.Default.DisabledMap;
        storageMap.GetOrCreate<TData>(ref this.pointId, ref this.storageObject);
        return this.storageObject;
    }
}
