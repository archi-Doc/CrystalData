// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

[TinyhandUnion("EmptyStorage", typeof(EmptyStorageConfiguration))]
[TinyhandUnion("SimpleStorage", typeof(SimpleStorageConfiguration))]
[TinyhandUnion("GlobalStorage", typeof(GlobalStorageConfiguration))]
public abstract partial record StorageConfiguration
{
    public StorageConfiguration(DirectoryConfiguration directoryConfiguration, DirectoryConfiguration? backupDirectoryConfiguration = null)
    {
        this.DirectoryConfiguration = directoryConfiguration;
        this.BackupDirectoryConfiguration = backupDirectoryConfiguration;
    }

    [Key("Directory")]
    public DirectoryConfiguration DirectoryConfiguration { get; init; }

    [Key("BackupDirectory")]
    public DirectoryConfiguration? BackupDirectoryConfiguration { get; init; }

    [KeyAsName]
    public int NumberOfHistoryFiles { get; init; } = 2;
}
