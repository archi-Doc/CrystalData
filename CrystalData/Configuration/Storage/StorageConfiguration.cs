// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Represents the base configuration for storage, including directory and backup settings.
/// </summary>
[TinyhandUnion("EmptyStorage", typeof(EmptyStorageConfiguration))]
[TinyhandUnion("SimpleStorage", typeof(SimpleStorageConfiguration))]
[TinyhandUnion("GlobalStorage", typeof(GlobalStorageConfiguration))]
public abstract partial record StorageConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageConfiguration"/> class.
    /// </summary>
    /// <param name="directoryConfiguration">The primary directory configuration for storage.</param>
    /// <param name="backupDirectoryConfiguration">The optional backup directory configuration.</param>
    public StorageConfiguration(DirectoryConfiguration directoryConfiguration, DirectoryConfiguration? backupDirectoryConfiguration = null)
    {
        this.DirectoryConfiguration = directoryConfiguration;
        this.BackupDirectoryConfiguration = backupDirectoryConfiguration;
    }

    /// <summary>
    /// Gets the primary directory configuration for storage.
    /// </summary>
    [Key("Directory")]
    public DirectoryConfiguration DirectoryConfiguration { get; init; }

    /// <summary>
    /// Gets the optional backup directory configuration.
    /// </summary>
    [Key("BackupDirectory")]
    public DirectoryConfiguration? BackupDirectoryConfiguration { get; init; }

    /// <summary>
    /// Gets the number of file histories (snapshots).<br/>
    /// Default value is 1.
    /// </summary>
    [KeyAsName]
    public int NumberOfHistoryFiles { get; init; } = 1;
}
