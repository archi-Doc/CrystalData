// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Filer;

/// <summary>
/// Defines asynchronous file operations for caller-supplied paths.
/// </summary>
/// <remarks>
/// Return memory obtained from reads after use. Writes share the supplied pooled memory;
/// callers retain their own reference and must not modify the bytes while a write is pending.
/// A timeout stops waiting and may leave the queued operation running.
/// </remarks>
public interface IFiler
{
    bool SupportsPartialWrite { get; }

    /// <summary>
    /// Prepare the filer and check if the path is valid.<br/>
    /// This method may be called multiple times.
    /// </summary>
    /// <param name="param"><see cref="PrepareParam"/>.</param>
    /// <param name="configuration"><see cref="PathConfiguration"/>.</param>
    /// <returns><see cref="CrystalResult"/>.</returns>
    Task<CrystalResult> PrepareAndCheck(PrepareParam param, PathConfiguration configuration);

    Task FlushAsync(bool terminate);

    Task<CrystalMemoryOwnerResult> ReadAsync(string path, long offset, int length, TimeSpan timeout);

    /// <summary>
    /// Queues a write. A result of <see cref="CrystalResult.Started"/> acknowledges submission, not persistence.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="offset">The byte offset at which to write.</param>
    /// <param name="dataToBeShared">The pooled memory to share with the queued operation.</param>
    /// <param name="truncate">Whether to truncate the file after the written bytes.</param>
    /// <returns>The submission result.</returns>
    CrystalResult WriteAndForget(string path, long offset, BytePool.RentedReadOnlyMemory dataToBeShared, bool truncate = true);

    Task<CrystalResult> WriteAsync(string path, long offset, BytePool.RentedReadOnlyMemory dataToBeShared, TimeSpan timeout, bool truncate = true);

    /// <summary>
    /// Queues deletion of the file. A result of <see cref="CrystalResult.Started"/> acknowledges submission only.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns><see cref="CrystalResult"/>.</returns>
    CrystalResult DeleteAndForget(string path);

    Task<CrystalResult> DeleteAsync(string path, TimeSpan timeout);

    Task<CrystalResult> DeleteDirectoryAsync(string path, bool recursive, TimeSpan timeout);

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
        => this.ReadAsync(path, offset, length, Timeout.InfiniteTimeSpan);

    Task<CrystalResult> WriteAsync(string path, long offset, BytePool.RentedReadOnlyMemory dataToBeShared, bool truncate = true)
        => this.WriteAsync(path, offset, dataToBeShared, Timeout.InfiniteTimeSpan, truncate);

    Task<CrystalResult> DeleteAsync(string path)
        => this.DeleteAsync(path, Timeout.InfiniteTimeSpan);

    Task<CrystalResult> DeleteDirectoryAsync(string path, bool recursive)
        => this.DeleteDirectoryAsync(path, recursive, Timeout.InfiniteTimeSpan);

    Task<List<PathInformation>> ListAsync(string path)
    => this.ListAsync(path, Timeout.InfiniteTimeSpan);

    #endregion
}
