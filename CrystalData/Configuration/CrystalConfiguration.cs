// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

[TinyhandObject(ImplicitKeyAsName = true, EnumAsString = true)]
public sealed partial record CrystalConfiguration
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromHours(1);
    public static readonly CrystalConfiguration Default = new();

    public CrystalConfiguration()
    {
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
    /// Gets the format for saving data, which can either be in binary or UTF8, with binary set as the default.
    /// </summary>
    public SaveFormat SaveFormat { get; init; }

    /// <summary>
    /// Gets the policy for saving data, with options such as manual saving, periodic saving, or not saving at all.
    /// </summary>
    public SavePolicy SavePolicy { get; init; }

    /// <summary>
    /// Gets the interval for automatic data saving.<br/>
    /// This is only effective when <see cref="SavePolicy"/> is set to <see cref="SavePolicy.Periodic"/>.
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
}
