// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

[TinyhandObject]
public partial record EmptyDirectoryConfiguration : DirectoryConfiguration
{
    public static readonly EmptyDirectoryConfiguration Default = new();

    public EmptyDirectoryConfiguration()
        : base()
    {
    }

    public EmptyDirectoryConfiguration(string directory)
        : base(directory)
    {
    }

    public override EmptyFileConfiguration CombineFile(string file)
        => new EmptyFileConfiguration(System.IO.Path.Combine(this.Path, PathHelper.GetPathNotRoot(file)));

    public override EmptyDirectoryConfiguration CombineDirectory(DirectoryConfiguration directory)
        => new EmptyDirectoryConfiguration(System.IO.Path.Combine(this.Path, PathHelper.GetPathNotRoot(directory.Path)));

    public override string ToString()
        => $"Empty directory";
}
