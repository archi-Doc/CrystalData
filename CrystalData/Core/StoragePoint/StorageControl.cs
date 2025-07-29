// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

using CrystalData.Internal;

namespace CrystalData;

public partial class StorageControl
{
    #region FiendAndProperty

    private readonly StoragePointClass.GoshujinClass storagePoints = new();

    #endregion

    internal StorageControl(Crystalizer crystalizer)
    {
    }

    public StoragePointClass GetOrCreate(ref ulong pointId, uint typeIdentifier)
    {
        using (this.storagePoints.LockObject.EnterScope())
        {
            if (pointId != 0 &&
                this.storagePoints.PointIdChain.TryGetValue(pointId, out var obj))
            {// Found existing StoragePoint.
                return obj;
            }

            while (true)
            {
                pointId = RandomVault.Default.NextUInt64();
                if (!this.storagePoints.PointIdChain.ContainsKey(pointId))
                {
                    break;
                }
            }

            obj = new StoragePointClass(pointId, typeIdentifier);
            return obj;
        }
    }
}
