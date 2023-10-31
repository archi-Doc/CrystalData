// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

[TinyhandObject]
public partial record EmptyFileConfiguration : FileConfiguration
{
    public static readonly EmptyFileConfiguration Default = new();

    public EmptyFileConfiguration()
        : base()
    {
    }

    public EmptyFileConfiguration(string file)
        : base(file)
    {
    }

    public override EmptyFileConfiguration AppendPath(string file)
        => new EmptyFileConfiguration(this.Path + file);

    public override string ToString()
        => $"Empty file";
}
