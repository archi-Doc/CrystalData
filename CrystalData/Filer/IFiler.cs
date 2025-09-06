// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData.Filer;

namespace CrystalData;

public interface IFiler
{
    bool SupportPartialWrite { get; }

    void SetTimeout(TimeSpan timeout);

    IFiler CloneWithExtension(string extension);

    /// <summary>
    /// Prepare the filer and check if the path is valid.<br/>
    /// This method may be called multiple times.
    /// </summary>
    /// <param name="param"><see cref="PrepareParam"/>.</param>
    /// <param name="configuration"><see cref="PathConfiguration"/>.</param>
    /// <returns><see cref="CrystalResult"/>.</returns>
    Task<CrystalResult> PrepareAndCheck(PrepareParam param, PathConfiguration configuration);

    Task<CrystalMemoryOwnerResult> ReadAsync(long offset, int length);

    Task<CrystalResult> WriteAsync(long offset, BytePool.RentReadOnlyMemory dataToBeShared, bool truncate = true);

    CrystalResult WriteAndForget(long offset, BytePool.RentReadOnlyMemory dataToBeShared, bool truncate = true);

    CrystalResult DeleteAndForget();

    Task<CrystalResult> DeleteAsync();
}
