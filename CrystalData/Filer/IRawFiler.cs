// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Filer;

public interface IRawFiler
{
    bool SupportPartialWrite { get; }

    /// <summary>
    /// Prepare the filer and check if the path is valid.<br/>
    /// This method may be called multiple times.
    /// </summary>
    /// <param name="param"><see cref="PrepareParam"/>.</param>
    /// <param name="configuration"><see cref="PathConfiguration"/>.</param>
    /// <returns><see cref="CrystalResult"/>.</returns>
    Task<CrystalResult> PrepareAndCheck(PrepareParam param, PathConfiguration configuration);

    Task TerminateAsync();

    Task<CrystalMemoryOwnerResult> ReadAsync(string path, long offset, int length, TimeSpan timeout);

    CrystalResult WriteAndForget(string path, long offset, ByteArrayPool.ReadOnlyMemoryOwner dataToBeShared, bool truncate = true);

    Task<CrystalResult> WriteAsync(string path, long offset, ByteArrayPool.ReadOnlyMemoryOwner dataToBeShared, TimeSpan timeout, bool truncate = true);

    /// <summary>
    /// Delete the file matching the path.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns><see cref="CrystalResult"/>.</returns>
    CrystalResult DeleteAndForget(string path);

    Task<CrystalResult> DeleteAsync(string path, TimeSpan timeout);

    Task<CrystalResult> DeleteDirectoryAsync(string path, TimeSpan timeout);

    /// <summary>
    /// List files and directories matching the path.
    /// </summary>
    /// <param name="path">Specify the path of the search criteria.<br/>
    /// Directory: "Directory/"<br/>
    /// Directory and prefix: "Directory/Prefix".</param>
    /// <param name="timeout">A <see cref="TimeSpan"/> that represents the number of milliseconds to wait, or a <see cref="TimeSpan"/> that represents -1 milliseconds to wait indefinitely.</param>
    /// <returns>A list of directories and files that match the search criteria.</returns>
    Task<List<PathInformation>> ListAsync(string path, TimeSpan timeout);

    #region InfiniteTimeout

    Task<CrystalMemoryOwnerResult> ReadAsync(string path, long offset, int length)
        => this.ReadAsync(path, offset, length, TimeSpan.MinValue);

    Task<CrystalResult> WriteAsync(string path, long offset, ByteArrayPool.ReadOnlyMemoryOwner dataToBeShared, bool truncate = true)
        => this.WriteAsync(path, offset, dataToBeShared, TimeSpan.MinValue, truncate);

    Task<CrystalResult> DeleteAsync(string path)
        => this.DeleteAsync(path, TimeSpan.MinValue);

    Task<CrystalResult> DeleteDirectoryAsync(string path)
        => this.DeleteDirectoryAsync(path, TimeSpan.MinValue);

    Task<List<PathInformation>> ListAsync(string path)
    => this.ListAsync(path, TimeSpan.MinValue);

    #endregion
}
