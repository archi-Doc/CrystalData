// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.IO;

namespace CrystalData;

[TinyhandObject]
public partial record S3DirectoryConfiguration : DirectoryConfiguration
{
    public S3DirectoryConfiguration()
        : this(string.Empty, string.Empty)
    {
    }

    public S3DirectoryConfiguration(string bucket, string directory)
        : base(directory)
    {
        this.Bucket = bucket;
    }

    [Key("Bucket")]
    public string Bucket { get; protected set; }

    public override S3FileConfiguration CombineFile(string file)
    {
        var newPath = PathHelper.CombineWithSlash(this.Path, PathHelper.GetPathNotRoot(file));
        return new S3FileConfiguration(this.Bucket, newPath);
    }

    public override S3DirectoryConfiguration CombineDirectory(DirectoryConfiguration directory)
    {
        var newPath = PathHelper.CombineWithSlash(this.Path, PathHelper.GetPathNotRoot(directory.Path));
        return new S3DirectoryConfiguration(this.Bucket, newPath);
    }

    public override string ToString()
        => $"S3 directory: {this.Bucket}/{this.Path}";
}
