// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

[TinyhandObject(ImplicitMemberNameAsKey = true, EnumAsString = true)]
public sealed partial record CrystalConfiguration
{
    public static readonly CrystalConfiguration Default = new();
    public static readonly TimeSpan DefaultSaveInterval = TimeSpan.FromHours(1); // 1 Hour

    public CrystalConfiguration()
    {
        this.SaveInterval = DefaultSaveInterval;
        this.FileConfiguration = EmptyFileConfiguration.Default;
    }

    public CrystalConfiguration(FileConfiguration fileConfiguration)
    {
        this.SaveInterval = DefaultSaveInterval;
        this.FileConfiguration = fileConfiguration;
        this.StorageConfiguration = EmptyStorageConfiguration.Default;
    }

    public CrystalConfiguration(FileConfiguration fileConfiguration, StorageConfiguration? storageConfiguration = null)
    {
        this.SaveInterval = DefaultSaveInterval;
        this.FileConfiguration = fileConfiguration;
        this.StorageConfiguration = storageConfiguration ?? EmptyStorageConfiguration.Default;
    }

    public CrystalConfiguration(SaveFormat saveFormat, FileConfiguration fileConfiguration, StorageConfiguration? storageConfiguration = null)
    {
        this.SaveFormat = saveFormat;
        this.SaveInterval = DefaultSaveInterval;
        this.FileConfiguration = fileConfiguration;
        this.StorageConfiguration = storageConfiguration ?? EmptyStorageConfiguration.Default;
    }

    /// <summary>
    /// Gets the format for saving data, which can either be in binary or UTF8.<br/>
    /// If not specified, <see cref="CrystalOptions.DefaultSaveFormat"/> (the default is binary) will be used..
    /// </summary>
    public SaveFormat SaveFormat { get; init; }

    /// <summary>
    /// Gets a value indicating whether the data is volatile.<br/>
    /// If <c>true</c>, the data is not persisted to storage and is only kept in memory.
    /// </summary>
    public bool Volatile { get; init; }

    /// <summary>
    /// Gets the interval for automatic data saving.
    /// </summary>
    public TimeSpan SaveInterval { get; init; }

    /// <summary>
    /// Gets the number of file histories (snapshots).<br/>
    /// Default value is 1.
    /// </summary>
    public int NumberOfFileHistories { get; init; } = 1;

    public FileConfiguration FileConfiguration { get; init; }

    public FileConfiguration? BackupFileConfiguration { get; init; }

    public StorageConfiguration StorageConfiguration { get; init; } = EmptyStorageConfiguration.Default;

    public bool RequiredForLoading { get; init; } = false;

    public bool HasFileHistories => this.NumberOfFileHistories > 0;

    [IgnoreMember]
    internal bool IsSingleton { get; set; }
}
