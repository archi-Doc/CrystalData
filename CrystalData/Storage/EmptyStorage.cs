// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Linq.Expressions;

namespace CrystalData.Storage;

public partial class EmptyStorage : IStorage
{
    public static readonly EmptyStorage Default = new();

    int IStorage.NumberOfHistoryFiles => 0;

    StorageMap IStorage.StorageMap => StorageMap.Disabled;

    long IStorage.StorageUsage => 0;

    void IStorage.SetTimeout(TimeSpan timeout)
        => Expression.Empty();

    Task<CrystalResult> IStorage.PrepareAndCheck(PrepareParam param, StorageConfiguration storageConfiguration)
        => Task.FromResult(CrystalResult.Success);

    Type IPersistable.DataType => throw new NotImplementedException();

    Task<CrystalResult> IPersistable.StoreData(StoreMode storeMode, CancellationToken cancellationToken)
        => Task.FromResult(CrystalResult.Success);

    Task<bool> IPersistable.TestJournal()
        => Task.FromResult(true);

    Task<CrystalMemoryOwnerResult> IStorage.GetAsync(ref ulong fileId)
        => Task.FromResult(new CrystalMemoryOwnerResult(CrystalResult.Success));

    CrystalResult IStorage.PutAndForget(ref ulong fileId, BytePool.RentReadOnlyMemory memoryToBeShared)
        => CrystalResult.Success;

    Task<CrystalResult> IStorage.PutAsync(ref ulong fileId, BytePool.RentReadOnlyMemory memoryToBeShared)
        => Task.FromResult(CrystalResult.Success);

    CrystalResult IStorage.DeleteAndForget(ref ulong fileId)
        => CrystalResult.Success;

    Task<CrystalResult> IStorage.DeleteAsync(ref ulong fileId)
        => Task.FromResult(CrystalResult.Success);

    Task<CrystalResult> IStorage.DeleteStorageAsync()
        => Task.FromResult(CrystalResult.Success);
}
