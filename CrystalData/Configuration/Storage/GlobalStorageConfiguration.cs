// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

[TinyhandObject]
public partial record GlobalStorageConfiguration : StorageConfiguration
{
    public static readonly GlobalStorageConfiguration Default = new();

    public GlobalStorageConfiguration()
        : base(EmptyDirectoryConfiguration.Default)
    {
    }
}
