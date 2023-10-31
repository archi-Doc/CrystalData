// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

[TinyhandObject]
public partial record GlobalFileConfiguration : FileConfiguration
{
    public GlobalFileConfiguration()
        : base()
    {
    }

    public GlobalFileConfiguration(string file)
        : base(file)
    {
    }

    public override GlobalFileConfiguration AppendPath(string file)
        => new GlobalFileConfiguration(this.Path + file);

    public override string ToString()
        => $"Global file: {this.Path}";
}
