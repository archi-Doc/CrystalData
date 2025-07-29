// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using CrystalData.Internal;
using Tinyhand.IO;

namespace CrystalData;

#pragma warning disable SA1204 // Static elements should appear before instance elements

/// <summary>
/// <see cref="StoragePoint{TData}"/> is an independent component of the data tree, responsible for loading and persisting data.
/// </summary>
/// <typeparam name="TData">The type of data.</typeparam>
[TinyhandObject(ExplicitKeyOnly = true)]
public partial class StoragePoint<TData> : ITinyhandSerializable<StoragePoint<TData>>, ITinyhandReconstructable<StoragePoint<TData>>, ITinyhandCloneable<StoragePoint<TData>>, IStructualObject, IStoragePoint
{
    #region FiendAndProperty

    private ulong pointId; // Lock:StorageControl
    private StorageObject? underlyingStorageObject;

    public Type DataType
        => typeof(TData);

    public uint TypeIdentifier
        => TinyhandTypeIdentifier.GetTypeIdentifier<TData>();

    /// <summary>
    /// Gets a value indicating whether storage is disabled, and data is serialized directly.
    /// </summary>
    public bool IsDisabled => this.GetOrCreate().IsDisabled;

    public bool IsLocked => this.GetOrCreate().IsLocked;

    public bool IsUnloading => this.GetOrCreate().IsUnloading;

    public bool IsUnloaded => this.GetOrCreate().IsUnloaded;

    public bool IsUnloadingOrUnloaded => this.GetOrCreate().IsUnloadingOrUnloaded;

    public bool CanUnload => this.GetOrCreate().CanUnload;

    #endregion

    public StoragePoint()
    {
    }

    public void DisableStorage()
        => this.GetOrCreate().ConfigureStorage(true);

    public void EnableStorage()
        => this.GetOrCreate().ConfigureStorage(false);

    public void Set(TData data) => this.GetOrCreate().Set(data);

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
        get => this.underlyingStorageObject?.StructualRoot;
        set
        {
            if (this.underlyingStorageObject is not null)
            {
                this.underlyingStorageObject.StructualRoot = value;
            }
        }
    }

    IStructualObject? IStructualObject.StructualParent
    {// Delegate properties to the underlying StorageObject.
        get => this.underlyingStorageObject?.StructualParent;
        set
        {
            if (this.underlyingStorageObject is not null)
            {
                this.underlyingStorageObject.StructualParent = value;
            }
        }
    }

    int IStructualObject.StructualKey
    {// Delegate properties to the underlying StorageObject.
        get => this.underlyingStorageObject is null ? 0 : this.underlyingStorageObject.StructualKey;
        set
        {
            if (this.underlyingStorageObject is not null)
            {
                this.underlyingStorageObject.StructualKey = value;
            }
        }
    }

    Type IStoragePoint.DataType => throw new NotImplementedException();

    void IStructualObject.SetupStructure(IStructualObject? parent, int key)
    {
        if (this.underlyingStorageObject is null &&
            parent?.StructualRoot is ICrystal crystal)
        {
            this.underlyingStorageObject = crystal.Crystalizer.StorageControl.GetOrCreate(ref this.pointId, this.TypeIdentifier);
            ((IStructualObject)this.underlyingStorageObject).SetParentAndKey(parent, key);
        }
    }

    #endregion

    #region Tinyhand

    static void ITinyhandSerializable<StoragePoint<TData>>.Serialize(ref TinyhandWriter writer, scoped ref StoragePoint<TData>? v, TinyhandSerializerOptions options)
    {
        if (v is null)
        {
            writer.WriteNil();
        }
        else if (v.underlyingStorageObject is null)
        {// In-class
            writer.Write(v.pointId);
        }
        else
        {// StorageObject
            v.underlyingStorageObject.SerializeStoragePoint(ref writer, options);
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
            var data = TinyhandSerializer.Deserialize<TData>(ref reader, options);
            var typeIdentifier = TinyhandTypeIdentifier.GetTypeIdentifier<TData>();
            if (v.underlyingStorageObject is null || v.underlyingStorageObject.TypeIdentifier != typeIdentifier)
            {
                v.underlyingStorageObject = new(typeIdentifier);
            }

            v.underlyingStorageObject.Set(data);
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
        else
        {
            var obj = new StoragePoint<TData>();
            obj.pointId = v.pointId;
            return obj;
        }
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
        if (this.underlyingStorageObject is not null)
        {
            return this.underlyingStorageObject;
        }

        /*if (((IStructualObject)this).StructualRoot is ICrystal crystal)
        {
            this.underlyingStorageObject = crystal.Crystalizer.StorageControl.GetOrCreate(ref this.pointId, this.TypeIdentifier);
            return this.underlyingStorageObject;
        }*/

        this.underlyingStorageObject = new(this.TypeIdentifier);
        return this.underlyingStorageObject;
    }
}
