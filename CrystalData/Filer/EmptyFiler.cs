// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Filer;

public partial class EmptyFiler : IFiler
{
    public static readonly EmptyFiler Default = new();

    bool IFiler.SupportPartialWrite => true;

    Task<CrystalResult> IFiler.PrepareAndCheck(PrepareParam param, PathConfiguration configuration)
        => Task.FromResult(CrystalResult.Success);

    Task IFiler.FlushAsync(bool terminate)
        => Task.CompletedTask;

    Task<CrystalMemoryOwnerResult> IFiler.ReadAsync(string path, long offset, int length, TimeSpan timeout)
        => Task.FromResult(new CrystalMemoryOwnerResult(CrystalResult.NotFound));

    CrystalResult IFiler.WriteAndForget(string path, long offset, BytePool.RentReadOnlyMemory dataToBeShared, bool truncate)
        => CrystalResult.Success;

    Task<CrystalResult> IFiler.WriteAsync(string path, long offset, BytePool.RentReadOnlyMemory dataToBeShared, TimeSpan timeout, bool truncate)
        => Task.FromResult(CrystalResult.Success);

    CrystalResult IFiler.DeleteAndForget(string path)
        => CrystalResult.Success;

    Task<CrystalResult> IFiler.DeleteAsync(string path, TimeSpan timeout)
        => Task.FromResult(CrystalResult.Success);

    Task<CrystalResult> IFiler.DeleteDirectoryAsync(string path, bool recursive, TimeSpan timeout)
        => Task.FromResult(CrystalResult.Success);

    Task<List<PathInformation>> IFiler.ListAsync(string path, TimeSpan timeout)
        => Task.FromResult(new List<PathInformation>());
}
