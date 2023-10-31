// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public enum SavePolicy
{
    /// <summary>
    /// Timing of saving data is controlled by the application [default].
    /// </summary>
    Manual,

    /// <summary>
    /// Data is volatile and not saved.
    /// </summary>
    Volatile,

    /// <summary>
    /// Data will be saved at regular intervals.
    /// </summary>
    Periodic,

    /// <summary>
    /// When the data is changed, it is registered in the save queue and will be saved in a second.
    /// </summary>
    OnChanged,
}

public enum SaveFormat
{
    Binary,
    Utf8,
}

[TinyhandObject(ImplicitKeyAsName = true, EnumAsString = true)]
public partial record CrystalConfiguration
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromHours(1);

    public static readonly CrystalConfiguration Default = new();

    public static CrystalConfiguration SingleUtf8(bool required, FileConfiguration fileConfiguration)
        => new CrystalConfiguration(SavePolicy.Manual, fileConfiguration) with
        { SaveFormat = SaveFormat.Utf8, NumberOfFileHistories = 0, RequiredForLoading = required, };

    public CrystalConfiguration()
    {
        this.SavePolicy = SavePolicy.Manual;
        this.SaveInterval = DefaultInterval;
        this.FileConfiguration = EmptyFileConfiguration.Default;
    }

    public CrystalConfiguration(SavePolicy savePolicy, FileConfiguration fileConfiguration, StorageConfiguration? storageConfiguration = null)
    {
        this.SavePolicy = savePolicy;
        this.SaveInterval = DefaultInterval;
        this.FileConfiguration = fileConfiguration;
        this.StorageConfiguration = storageConfiguration ?? EmptyStorageConfiguration.Default;
    }

    /// <summary>
    /// Gets the format of the data to save.<br/>
    /// It can be binary or UTF8, with binary being the default.
    /// </summary>
    public SaveFormat SaveFormat { get; init; }

    public SavePolicy SavePolicy { get; init; }

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
}
