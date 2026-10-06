// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Defines asynchronous operations for one preconfigured file.
/// </summary>
/// <remarks>
/// Return read-result memory after use. Writes share the supplied memory without taking the caller's reference.
/// A timeout can return before the queued operation finishes; keep the bytes unchanged until it completes.
/// Methods ending in <c>AndForget</c> acknowledge submission only.
/// </remarks>
public interface ISingleFiler
{
    bool SupportsPartialWrite { get; }

    void SetTimeout(TimeSpan timeout);

    ISingleFiler CloneWithExtension(string extension);

    /// <summary>
    /// Prepare the filer and check if the path is valid.<br/>
    /// This method may be called multiple times.
    /// </summary>
    /// <param name="param"><see cref="PrepareParam"/>.</param>
    /// <param name="configuration"><see cref="PathConfiguration"/>.</param>
    /// <returns><see cref="CrystalResult"/>.</returns>
    Task<CrystalResult> PrepareAndCheck(PrepareParam param, PathConfiguration configuration);

    Task<CrystalMemoryOwnerResult> ReadAsync(long offset, int length);

    Task<CrystalResult> WriteAsync(long offset, BytePool.RentedReadOnlyMemory dataToBeShared, bool truncate = true);

    CrystalResult WriteAndForget(long offset, BytePool.RentedReadOnlyMemory dataToBeShared, bool truncate = true);

    CrystalResult DeleteAndForget();

    Task<CrystalResult> DeleteAsync();
}
