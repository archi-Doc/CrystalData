// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.IO;

namespace CrystalData;

[TinyhandUnion("EmptyDirectory", typeof(EmptyDirectoryConfiguration))]
[TinyhandUnion("LocalDirectory", typeof(LocalDirectoryConfiguration))]
[TinyhandUnion("S3Directory", typeof(S3DirectoryConfiguration))]
[TinyhandUnion("GlobalDirectory", typeof(GlobalDirectoryConfiguration))]
public abstract partial record DirectoryConfiguration : PathConfiguration
{
    public DirectoryConfiguration()
        : base()
    {
    }

    public DirectoryConfiguration(string directory)
        : base(StorageHelper.EndsWithSlashOrBackslash(directory) ? directory : directory + StorageHelper.Slash)
    {
    }

    public override Type PathType => Type.Directory;

    public abstract FileConfiguration CombineFile(string file);

    public abstract DirectoryConfiguration CombineDirectory(DirectoryConfiguration directory);

    public override string ToString()
        => $"Directory: {this.Path}";
}
