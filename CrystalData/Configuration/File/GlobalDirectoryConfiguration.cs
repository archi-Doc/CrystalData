// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

[TinyhandObject]
public partial record GlobalDirectoryConfiguration : DirectoryConfiguration
{
    public GlobalDirectoryConfiguration()
    : base()
    {
    }

    public GlobalDirectoryConfiguration(string directory)
        : base(directory)
    {
    }

    public override GlobalFileConfiguration CombineFile(string file)
        => new GlobalFileConfiguration(StorageHelper.CombineWithSlash(this.Path, StorageHelper.GetPathNotRoot(file)));

    public override GlobalDirectoryConfiguration CombineDirectory(DirectoryConfiguration directory)
        => new GlobalDirectoryConfiguration(StorageHelper.CombineWithSlash(this.Path, StorageHelper.GetPathNotRoot(directory.Path)));

    public override string ToString()
        => $"Global directory: {this.Path}";
}
