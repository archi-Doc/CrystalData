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
public partial struct StoragePoint<TData> : ITinyhandSerializable<StoragePoint<TData>>, ITinyhandReconstructable<StoragePoint<TData>>, ITinyhandCloneable<StoragePoint<TData>>, IStructualObject, IStoragePoint
{
    #region FiendAndProperty

    public Type DataType
        => typeof(TData);

    public uint TypeIdentifier
        => TinyhandTypeIdentifier.GetTypeIdentifier<TData>();

    private ulong pointId;

    private StoragePointClass? underlyingStoragePoint;

    private StoragePointClass UnderlyingStoragePoint
    {
        get
        {
            if (this.underlyingStoragePoint is not null)
            {
                return this.underlyingStoragePoint;
            }

            if (((IStructualObject)this).StructualRoot is ICrystal crystal)
            {
                this.underlyingStoragePoint = crystal.Crystalizer.StorageControl.GetOrCreate(ref this.pointId);
                return this.underlyingStoragePoint;
            }

            this.underlyingStoragePoint = new(0, 0);
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

    public void DisableStorage()
        => this.UnderlyingStoragePoint.ConfigureStorage(true);

    public void EnableStorage()
        => this.UnderlyingStoragePoint.ConfigureStorage(false);

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

    Type IStoragePoint.DataType => throw new NotImplementedException();

    void IStructualObject.SetupStructure(IStructualObject? parent, int key)
    {//
        ((IStructualObject)this).SetParentAndKey(parent, key);
    }

    #endregion

    static void ITinyhandSerializable<StoragePoint<TData>>.Serialize(ref TinyhandWriter writer, scoped ref StoragePoint<TData> v, TinyhandSerializerOptions options)
    {
        // writer.Write(v.pointId);
        v.UnderlyingStoragePoint.SerializeStoragePoint(ref writer, options);
    }

    static unsafe void ITinyhandSerializable<StoragePoint<TData>>.Deserialize(ref TinyhandReader reader, scoped ref StoragePoint<TData> v, TinyhandSerializerOptions options)
    {
        // If the type is interger, it is treated as PointId; otherwise, deserialization is attempted as TData (since TData is not expected to be of interger type, this should generally work without issue).
        if (reader.TryReadUInt64(out var pointId))
        {
            v.pointId = pointId;
        }
        else
        {
            var data = TinyhandSerializer.Deserialize<TData>(ref reader, options);
            v.underlyingStoragePoint = new(0, TinyhandTypeIdentifier.GetTypeIdentifier<TData>());
            // v.underlyingStoragePoint.data = data;
            v.underlyingStoragePoint.Set(data);
        }
    }

    static unsafe void ITinyhandReconstructable<StoragePoint<TData>>.Reconstruct([NotNull] scoped ref StoragePoint<TData> v, TinyhandSerializerOptions options)
    {
        v = default;
    }

    static unsafe StoragePoint<TData> ITinyhandCloneable<StoragePoint<TData>>.Clone(scoped ref StoragePoint<TData> v, TinyhandSerializerOptions options)
    {
        return new(v.pointId);
    }

    public async ValueTask<TData?> TryGet()
    {
        var data = await this.UnderlyingStoragePoint.TryGet().ConfigureAwait(false);
        return (TData?)data;
    }

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

    Task<bool> IStoragePoint.Save(UnloadMode2 unloadMode)
    {
        throw new NotImplementedException();
    }

    bool IStoragePoint.Probe(ProbeMode probeMode)
    {
        throw new NotImplementedException();
    }
}
