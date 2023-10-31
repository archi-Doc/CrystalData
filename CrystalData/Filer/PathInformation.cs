// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Filer;

public readonly struct PathInformation
{
    public PathInformation(string file, long length)
    {// File
        this.Path = file;
        this.Length = length;
    }

    public PathInformation(string directory)
    {// Directory
        this.Path = directory;
        this.Length = -1;
    }

    public readonly string Path;
    public readonly long Length;

    public bool IsFile => this.Length >= 0;

    public bool IsDirectory => this.Length < 0;

    public override string ToString()
        => $"{this.Path} ({this.Length})";
}
