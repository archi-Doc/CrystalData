// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Linq.Expressions;

namespace CrystalData.Storage;

public partial class EmptyStorage : IStorage
{
    public static readonly EmptyStorage Default = new();

    long IStorage.StorageUsage => 0;

    void IStorage.SetTimeout(TimeSpan timeout)
        => Expression.Empty();

    Task<CrystalResult> IStorage.PrepareAndCheck(PrepareParam param, StorageConfiguration storageConfiguration)
        => Task.FromResult(CrystalResult.Success);

    Task IStorage.SaveStorage(ICrystal? callingCrystal)
        => Task.CompletedTask;

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
