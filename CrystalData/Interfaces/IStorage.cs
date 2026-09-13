// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Defines auxiliary object storage used by structural data and <see cref="StoragePoint{TData}"/>.
/// </summary>
public interface IStorage : IPersistable
{
    int NumberOfHistoryFiles { get; }

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

    CrystalResult PutAndForget(ref ulong fileId, BytePool.RentedReadOnlyMemory dataToBeShared);

    Task<CrystalResult> PutAsync(ref ulong fileId, BytePool.RentedReadOnlyMemory dataToBeShared);

    CrystalResult DeleteAndForget(ref ulong fileId);

    Task<CrystalResult> DeleteAsync(ref ulong fileId);

    Task<CrystalResult> DeleteStorageAsync();

    void Dump()
    {
    }
}
