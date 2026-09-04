// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Disables auxiliary storage for a crystal.
/// </summary>
[TinyhandObject]
public partial record EmptyStorageConfiguration : StorageConfiguration
{
    public static readonly EmptyStorageConfiguration Default = new();

    public EmptyStorageConfiguration()
        : base(EmptyDirectoryConfiguration.Default)
    {
    }
}
