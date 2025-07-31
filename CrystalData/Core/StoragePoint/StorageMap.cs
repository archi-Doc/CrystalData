// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

using System.Diagnostics.CodeAnalysis;
using CrystalData.Internal;

namespace CrystalData;

[TinyhandObject]
public sealed partial class StorageMap
{
    public const string Filename = "Map";
    public static readonly StorageMap Invalid = new();

    #region FiendAndProperty

    [Key(0)]
    private StorageObject.GoshujinClass storagePoints = new();

    [IgnoreMember]
    public StorageControl? StorageControl { get; private set; }

    [MemberNotNullWhen(true, nameof(StorageControl))]
    public bool IsValid => this.StorageControl is not null;

    public bool IsInvalid => this.StorageControl is null;

    #endregion

    public StorageMap()
    {
    }

    public void Initialize(StorageControl storageControl)
    {
        this.StorageControl = storageControl;
    }

    public void Update(ulong pointId)
    {
        if (this.IsInvalid)
        {
            return;
        }

        if (pointId == 0)
        {
            return;
        }

        using (this.storagePoints.LockObject.EnterScope())
        {
            if (this.storagePoints.PointIdChain.TryGetValue(pointId, out var obj))
            {// Found
                this.storagePoints.LastAccessedChain.AddFirst(obj);
            }
        }
    }

    public void GetOrCreate<TData>(ref ulong pointId, [NotNull] ref StorageObject? storageObject)
    {
        if (this.IsInvalid)
        {
            lock (this)
            {
                if (storageObject is not null)
                {
                    return;
                }

                var typeIdentifier = TinyhandTypeIdentifier.GetTypeIdentifier<TData>();
                storageObject = new StorageObject(typeIdentifier);
                pointId = 0;
                return;
            }
        }

        using (this.storagePoints.LockObject.EnterScope())
        {
            if (storageObject is not null)
            {
                return;
            }

            var id = pointId;
            if (id != 0 &&
                this.storagePoints.PointIdChain.TryGetValue(id, out storageObject!))
            {// Found existing StoragePoint.
                return;
            }

            while (true)
            {
                id = RandomVault.Default.NextUInt64();
                if (!this.storagePoints.PointIdChain.ContainsKey(id))
                {
                    break;
                }
            }

            var typeIdentifier2 = TinyhandTypeIdentifier.GetTypeIdentifier<TData>();
            storageObject = new StorageObject(id, typeIdentifier2);
            storageObject.Goshujin = this.storagePoints;
            pointId = id;
        }
    }
}
