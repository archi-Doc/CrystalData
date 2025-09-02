// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

// [TinyhandObject(ImplicitKeyAsName = true, AddImmutable = true)]
public partial record class CrystalizerOptions
{
    public const int DefaultMemoryUsageLimit = 1024 * 1024 * 500; // 500MB

    public CrystalizerOptions()
    {
        this.FilerTimeout = TimeSpan.MinValue; // TimeSpan.FromSeconds(3);
        this.TimeoutUntilForcedRelease = TimeSpan.FromSeconds(10);
        this.DefaultSaveInterval = CrystalConfiguration.DefaultSaveInterval;
    }

    public bool EnableFilerLogger { get; init; } = false;

    public string DataDirectory { get; init; } = string.Empty;

    public TimeSpan FilerTimeout { get; init; }

    public long MemoryUsageLimit { get; init; } = DefaultMemoryUsageLimit;

    public int ConcurrentUnload { get; init; } = 8;

    public TimeSpan TimeoutUntilForcedRelease { get; init; }

    public SaveFormat DefaultSaveFormat { get; init; } = SaveFormat.Binary;

    public SavePolicy DefaultSavePolicy { get; init; } = SavePolicy.Manual;

    public TimeSpan DefaultSaveInterval { get; init; }

    public FileConfiguration? SupplementFile { get; init; }

    public FileConfiguration? BackupSupplementFile { get; init; }

    public DirectoryConfiguration GlobalDirectory { get; init; } = new LocalDirectoryConfiguration();

    public DirectoryConfiguration? DefaultBackup { get; init; }

    public StorageConfiguration GlobalStorage { get; init; } = new SimpleStorageConfiguration(new LocalDirectoryConfiguration("Storage"));
}
