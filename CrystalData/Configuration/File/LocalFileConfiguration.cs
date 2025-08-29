// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Represents a configuration for a local file.<br/>
/// Specifies the file path relative to the data directory, defined by <see cref="UnitOptions.DataDirectory"/> or the current directory.
/// </summary>
[TinyhandObject]
public partial record LocalFileConfiguration : FileConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalFileConfiguration"/> class.
    /// </summary>
    public LocalFileConfiguration()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalFileConfiguration"/> class with the specified file path.
    /// </summary>
    /// <param name="file">The file path.</param>
    public LocalFileConfiguration(string file)
        : base(file)
    {
    }

    /// <summary>
    /// Appends the specified file path to the current path and returns a new <see cref="LocalFileConfiguration"/> instance.
    /// </summary>
    /// <param name="file">The file path relative to the data directory, defined by <see cref="UnitOptions.DataDirectory"/> or the current directory.</param>
    /// <returns>A new <see cref="LocalFileConfiguration"/> with the appended path.</returns>
    public override LocalFileConfiguration AppendPath(string file)
        => new LocalFileConfiguration(this.Path + file);

    /// <summary>
    /// Returns a string that represents the current local file configuration.
    /// </summary>
    /// <returns>A string representation of the local file configuration.</returns>
    public override string ToString()
        => $"Local file: {this.Path}";
}
