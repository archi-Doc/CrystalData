// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

[TinyhandUnion("EmptyFile", typeof(EmptyFileConfiguration))]
[TinyhandUnion("EmptyDirectory", typeof(EmptyDirectoryConfiguration))]
[TinyhandUnion("LocalFile", typeof(LocalFileConfiguration))]
[TinyhandUnion("LocalDirectory", typeof(LocalDirectoryConfiguration))]
[TinyhandUnion("S3File", typeof(S3FileConfiguration))]
[TinyhandUnion("S3Directory", typeof(S3DirectoryConfiguration))]
[TinyhandUnion("GlobalFile", typeof(GlobalFileConfiguration))]
[TinyhandUnion("GlobalDirectory", typeof(GlobalDirectoryConfiguration))]
public abstract partial record PathConfiguration
{
    public enum Type
    {
        Unknown,
        File,
        Directory,
    }

    public PathConfiguration()
        : this(string.Empty)
    {
    }

    public PathConfiguration(string path)
    {
        this.Path = path;
    }

    public virtual Type PathType => Type.Unknown;

    [Key("Path")]
    public string Path { get; protected set; }

    public bool IsPathRooted
        => System.IO.Path.IsPathRooted(this.Path);

    public override string ToString()
        => $"Path: {this.Path}";
}
