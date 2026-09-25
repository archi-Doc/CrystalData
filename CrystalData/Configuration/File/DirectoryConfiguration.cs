// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.IO;

namespace CrystalData;

/// <summary>
/// Defines a directory path and operations for creating child path configurations.
/// </summary>
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
        : base(directory.Length == 0 || StorageHelper.EndsWithSlashOrBackslash(directory) ? directory : directory + StorageHelper.Slash)
    {// An empty directory stays relative ("/" would be the root directory).
    }

    public override PathKind Kind => PathKind.Directory;

    public abstract FileConfiguration CombineFile(string file);

    public abstract DirectoryConfiguration CombineDirectory(DirectoryConfiguration directory);

    public override string ToString()
        => $"Directory: {this.Path}";
}
