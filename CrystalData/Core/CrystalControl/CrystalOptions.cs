// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Defines global runtime defaults and resource limits for <see cref="CrystalControl"/>.
/// </summary>
public partial record class CrystalOptions
{
    public const int DefaultMemoryUsageLimit = 1024 * 1024 * 500; // 500MB
    public static readonly TimeSpan DefaultSaveInterval = TimeSpan.FromHours(1);
    public static readonly TimeSpan DefaultSaveDelay = TimeSpan.FromMinutes(1);

    public CrystalOptions()
    {
        this.FilerTimeout = TimeSpan.FromSeconds(3); // await this.Add(job).ConfigureAwait(false);
        this.TimeoutUntilForcedRelease = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Gets a value indicating whether file-operation logging is enabled.
    /// </summary>
    public bool EnableFilerLogger { get; init; } = false;

    /// <summary>
    /// Gets the base directory for relative local paths, including default supplement metadata.
    /// </summary>
    public string DataDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timeout for waiting on file operations. Timed-out operations may still complete.
    /// </summary>
    public TimeSpan FilerTimeout { get; init; }

    /// <summary>
    /// Gets the auxiliary-data memory threshold in bytes. Locked or pinned data may keep usage above it.
    /// </summary>
    public long MemoryUsageLimit { get; init; } = DefaultMemoryUsageLimit;

    /// <summary>
    /// Gets the maximum number of concurrent workers used by bulk save and release operations.
    /// </summary>
    public int MaxConcurrentUnloads { get; init; } = 8;

    /// <summary>
    /// Gets the time a bulk release retries before switching to forced release.
    /// </summary>
    public TimeSpan TimeoutUntilForcedRelease { get; init; }

    /// <summary>
    /// Gets the default delay for saves requested through the save queue.
    /// </summary>
    public TimeSpan SaveDelay { get; init; } = DefaultSaveDelay;

    /// <summary>
    /// Gets the interval exposed by the auxiliary-storage controller.
    /// </summary>
    /// <remarks>This value currently does not schedule saves. Snapshot scheduling uses <see cref="CrystalConfiguration.SaveInterval"/>.</remarks>
    public TimeSpan SaveInterval { get; init; } = DefaultSaveInterval;

    /// <summary>
    /// Gets the snapshot format used when a crystal specifies <see cref="SaveFormat.Default"/>.
    /// </summary>
    public SaveFormat DefaultSaveFormat { get; init; } = SaveFormat.Binary;

    /// <summary>
    /// Gets the directory used to derive backup paths when no explicit backup is configured.
    /// </summary>
    public DirectoryConfiguration? DefaultBackupDirectory { get; init; }

    /// <summary>
    /// Gets the supplement metadata file, or uses the default local file when unspecified.
    /// </summary>
    public FileConfiguration? SupplementFile { get; init; }

    /// <summary>
    /// Gets the backup supplement file, or derives it from the default backup directory when unspecified.
    /// </summary>
    public FileConfiguration? BackupSupplementFile { get; init; }

    /// <summary>
    /// Gets the base directory for global file and directory configurations.
    /// </summary>
    public DirectoryConfiguration GlobalDirectory { get; init; } = new LocalDirectoryConfiguration();

    /// <summary>
    /// Gets the auxiliary storage selected by <see cref="GlobalStorageConfiguration"/>.
    /// </summary>
    public StorageConfiguration GlobalStorage { get; init; } = new SimpleStorageConfiguration(new LocalDirectoryConfiguration("Storage"));
}
