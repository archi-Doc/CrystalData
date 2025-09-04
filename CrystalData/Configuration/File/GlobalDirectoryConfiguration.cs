// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Represents a global directory configuration, inheriting from <see cref="DirectoryConfiguration"/>.<br/>
/// Specifies the directory path relative to the common root directory, as defined by <see cref="CrystalizerOptions.GlobalDirectory"/>.
/// </summary>
[TinyhandObject]
public partial record GlobalDirectoryConfiguration : DirectoryConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalDirectoryConfiguration"/> class with an empty path.
    /// </summary>
    public GlobalDirectoryConfiguration()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalDirectoryConfiguration"/> class with the specified directory path.
    /// </summary>
    /// <param name="directory">The directory path relative to the common root directory, as defined by <see cref="CrystalizerOptions.GlobalDirectory"/>.</param>
    public GlobalDirectoryConfiguration(string directory)
        : base(directory)
    {
    }

    /// <summary>
    /// Combines the current directory path with the specified file path and returns a new <see cref="GlobalFileConfiguration"/> instance.
    /// </summary>
    /// <param name="file">The file path to combine.</param>
    /// <returns>A new <see cref="GlobalFileConfiguration"/> with the combined path.</returns>
    public override GlobalFileConfiguration CombineFile(string file)
        => new GlobalFileConfiguration(StorageHelper.CombineWithSlash(this.Path, StorageHelper.GetPathNotRoot(file)));

    /// <summary>
    /// Combines the current directory path with the specified <see cref="DirectoryConfiguration"/> and returns a new <see cref="GlobalDirectoryConfiguration"/> instance.
    /// </summary>
    /// <param name="directory">The directory configuration to combine.</param>
    /// <returns>A new <see cref="GlobalDirectoryConfiguration"/> with the combined path.</returns>
    public override GlobalDirectoryConfiguration CombineDirectory(DirectoryConfiguration directory)
        => new GlobalDirectoryConfiguration(StorageHelper.CombineWithSlash(this.Path, StorageHelper.GetPathNotRoot(directory.Path)));

    /// <summary>
    /// Returns a string that represents the current <see cref="GlobalDirectoryConfiguration"/>.
    /// </summary>
    /// <returns>A string representation of the global directory configuration.</returns>
    public override string ToString()
        => $"Global directory: {this.Path}";
}
