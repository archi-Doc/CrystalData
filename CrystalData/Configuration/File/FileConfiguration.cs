// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

[TinyhandUnion("EmptyFile", typeof(EmptyFileConfiguration))]
[TinyhandUnion("LocalFile", typeof(LocalFileConfiguration))]
[TinyhandUnion("S3File", typeof(S3FileConfiguration))]
[TinyhandUnion("GlobalFile", typeof(GlobalFileConfiguration))]
public abstract partial record FileConfiguration : PathConfiguration, IEquatable<FileConfiguration>
{
    public FileConfiguration()
        : base()
    {
    }

    public FileConfiguration(string file)
        : base(file)
    {
    }

    public string DirectoryName
    {
        get
        {
            try
            {
                return System.IO.Path.GetDirectoryName(this.Path) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public string FileName
    {
        get
        {
            try
            {
                return System.IO.Path.GetFileName(this.Path) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public override Type PathType => Type.File;

    public abstract FileConfiguration AppendPath(string file);

    public override string ToString()
        => $"File: {this.Path}";
}
