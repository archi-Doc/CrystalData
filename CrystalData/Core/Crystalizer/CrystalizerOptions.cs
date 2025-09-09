// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

// [TinyhandObject(ImplicitKeyAsName = true, AddImmutable = true)]
public partial record class CrystalizerOptions
{
    public const int DefaultMemoryUsageLimit = 1024 * 1024 * 500; // 500MB
    public static readonly TimeSpan DefaultSaveInterval = TimeSpan.FromHours(1);
    public static readonly TimeSpan DefaultSaveDelay = TimeSpan.FromMinutes(1);

    public CrystalizerOptions()
    {
        this.FilerTimeout = TimeSpan.MinValue; // TimeSpan.FromSeconds(3);
        this.TimeoutUntilForcedRelease = TimeSpan.FromSeconds(10);
    }

    public bool EnableFilerLogger { get; init; } = false;

    public string DataDirectory { get; init; } = string.Empty;

    public TimeSpan FilerTimeout { get; init; }

    public long MemoryUsageLimit { get; init; } = DefaultMemoryUsageLimit;

    public int ConcurrentUnload { get; init; } = 8;

    public TimeSpan TimeoutUntilForcedRelease { get; init; }

    public TimeSpan SaveDelay { get; init; } = DefaultSaveDelay;

    public TimeSpan SaveInterval { get; init; } = DefaultSaveInterval;

    public SaveFormat DefaultSaveFormat { get; init; } = SaveFormat.Binary;

    public DirectoryConfiguration? DefaultBackup { get; init; }

    public FileConfiguration? SupplementFile { get; init; }

    public FileConfiguration? BackupSupplementFile { get; init; }

    public DirectoryConfiguration GlobalDirectory { get; init; } = new LocalDirectoryConfiguration();

    public StorageConfiguration GlobalStorage { get; init; } = new SimpleStorageConfiguration(new LocalDirectoryConfiguration("Storage"));
}
