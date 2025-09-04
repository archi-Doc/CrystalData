// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Represents a configuration for a local directory.<br/>
/// Specifies the directory path relative to the data directory, defined by <see cref="UnitOptions.DataDirectory"/> or the current directory.
/// </summary>
[TinyhandObject]
public partial record LocalDirectoryConfiguration : DirectoryConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDirectoryConfiguration"/> class.
    /// </summary>
    public LocalDirectoryConfiguration()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDirectoryConfiguration"/> class with the specified directory path.
    /// </summary>
    /// <param name="directory">The directory path relative to the data directory, defined by <see cref="UnitOptions.DataDirectory"/> or the current directory.</param>
    public LocalDirectoryConfiguration(string directory)
        : base(directory)
    {
    }

    /// <summary>
    /// Combines the current directory path with the specified file name and returns a <see cref="LocalFileConfiguration"/>.
    /// </summary>
    /// <param name="file">The file name or relative path to combine.</param>
    /// <returns>A <see cref="LocalFileConfiguration"/> representing the combined path.</returns>
    public override LocalFileConfiguration CombineFile(string file)
        => new LocalFileConfiguration(System.IO.Path.Combine(this.Path, StorageHelper.GetPathNotRoot(file)));

    /// <summary>
    /// Combines the current directory path with another directory configuration and returns a <see cref="LocalDirectoryConfiguration"/>.
    /// </summary>
    /// <param name="directory">The directory configuration to combine.</param>
    /// <returns>A <see cref="LocalDirectoryConfiguration"/> representing the combined path.</returns>
    public override LocalDirectoryConfiguration CombineDirectory(DirectoryConfiguration directory)
        => new LocalDirectoryConfiguration(System.IO.Path.Combine(this.Path, StorageHelper.GetPathNotRoot(directory.Path)));

    /// <summary>
    /// Returns a string that represents the current local directory configuration.
    /// </summary>
    /// <returns>A string representation of the local directory.</returns>
    public override string ToString()
        => $"Local directory: {this.Path}";
}
