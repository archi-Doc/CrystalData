// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

[TinyhandObject]
public partial record LocalFileConfiguration : FileConfiguration
{
    public LocalFileConfiguration()
    : base()
    {
    }

    public LocalFileConfiguration(string file)
        : base(file)
    {
    }

    public override LocalFileConfiguration AppendPath(string file)
        => new LocalFileConfiguration(this.Path + file);

    public override string ToString()
        => $"Local file: {this.Path}";
}
