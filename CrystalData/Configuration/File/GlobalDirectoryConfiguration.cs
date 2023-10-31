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
        => new GlobalFileConfiguration(PathHelper.CombineWithSlash(this.Path, PathHelper.GetPathNotRoot(file)));

    public override GlobalDirectoryConfiguration CombineDirectory(DirectoryConfiguration directory)
        => new GlobalDirectoryConfiguration(PathHelper.CombineWithSlash(this.Path, PathHelper.GetPathNotRoot(directory.Path)));

    public override string ToString()
        => $"Global directory: {this.Path}";
}
