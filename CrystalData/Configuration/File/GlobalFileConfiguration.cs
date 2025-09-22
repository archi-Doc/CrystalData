// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Represents a global file configuration, inheriting from <see cref="FileConfiguration"/>.<br/>
/// Specifies the file path relative to the common root directory, as defined by <see cref="CrystalOptions.GlobalDirectory"/>.
/// </summary>
[TinyhandObject]
public partial record GlobalFileConfiguration : FileConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalFileConfiguration"/> class with an empty path.
    /// </summary>
    public GlobalFileConfiguration()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalFileConfiguration"/> class with the specified file path.
    /// </summary>
    /// <param name="file">The file path relative to the common root directory, as defined by <see cref="CrystalOptions.GlobalDirectory"/>.</param>
    public GlobalFileConfiguration(string file)
        : base(file)
    {
    }

    /// <summary>
    /// Appends the specified file path to the current path and returns a new <see cref="GlobalFileConfiguration"/> instance.
    /// </summary>
    /// <param name="file">The file path to append.</param>
    /// <returns>A new <see cref="GlobalFileConfiguration"/> with the appended path.</returns>
    public override GlobalFileConfiguration AppendPath(string file)
        => new GlobalFileConfiguration(this.Path + file);

    /// <summary>
    /// Returns a string that represents the current <see cref="GlobalFileConfiguration"/>.
    /// </summary>
    /// <returns>A string representation of the global file configuration.</returns>
    public override string ToString()
        => $"Global file: {this.Path}";
}
