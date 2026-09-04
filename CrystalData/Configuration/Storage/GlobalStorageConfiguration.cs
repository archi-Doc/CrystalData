// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Selects the shared storage configuration from <see cref="CrystalOptions.GlobalStorage"/>.
/// </summary>
[TinyhandObject]
public partial record GlobalStorageConfiguration : StorageConfiguration
{
    public static readonly GlobalStorageConfiguration Default = new();

    public GlobalStorageConfiguration()
        : base(EmptyDirectoryConfiguration.Default)
    {
    }
}
