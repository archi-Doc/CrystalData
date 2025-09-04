// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public interface IStorage : IPersistable
{
    StorageMap StorageMap { get; }

    long StorageUsage { get; }

    void SetTimeout(TimeSpan timeout);

    /// <summary>
    /// Prepare the storage.<br/>
    /// This method may be called multiple times.
    /// </summary>
    /// <param name="param"><see cref="PrepareParam"/>.</param>
    /// <param name="storageConfiguration"><see cref="StorageConfiguration"/>.</param>
    /// <returns><see cref="CrystalResult"/>.</returns>
    Task<CrystalResult> PrepareAndCheck(PrepareParam param, StorageConfiguration storageConfiguration);

    Task<CrystalMemoryOwnerResult> GetAsync(ref ulong fileId);

    CrystalResult PutAndForget(ref ulong fileId, BytePool.RentReadOnlyMemory memoryToBeShared);

    Task<CrystalResult> PutAsync(ref ulong fileId, BytePool.RentReadOnlyMemory memoryToBeShared);

    CrystalResult DeleteAndForget(ref ulong fileId);

    Task<CrystalResult> DeleteAsync(ref ulong fileId);

    Task<CrystalResult> DeleteStorageAsync();

    void Dump()
    {
    }
}
