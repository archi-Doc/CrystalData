// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

[TinyhandObject]
public partial record LocalDirectoryConfiguration : DirectoryConfiguration
{
    public LocalDirectoryConfiguration()
    : base()
    {
    }

    public LocalDirectoryConfiguration(string directory)
        : base(directory)
    {
    }

    public override LocalFileConfiguration CombineFile(string file)
        => new LocalFileConfiguration(System.IO.Path.Combine(this.Path, StorageHelper.GetPathNotRoot(file)));

    public override LocalDirectoryConfiguration CombineDirectory(DirectoryConfiguration directory)
        => new LocalDirectoryConfiguration(System.IO.Path.Combine(this.Path, StorageHelper.GetPathNotRoot(directory.Path)));

    public override string ToString()
        => $"Local directory: {this.Path}";
}
