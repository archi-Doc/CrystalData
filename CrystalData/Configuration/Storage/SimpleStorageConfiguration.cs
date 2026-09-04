// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Configures file-backed auxiliary storage and an optional backup directory.
/// </summary>
[TinyhandObject]
public partial record SimpleStorageConfiguration : StorageConfiguration
{
    public SimpleStorageConfiguration(DirectoryConfiguration configuration, DirectoryConfiguration? backupConfiguration = null)
        : base(configuration, backupConfiguration)
    {
    }
}
