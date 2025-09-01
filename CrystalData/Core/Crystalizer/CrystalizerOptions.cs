// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public class CrystalizerOptions
{
    public const int DefaultMemoryUsageLimit = 1024 * 1024 * 500; // 500MB

    public CrystalizerOptions()
    {
        this.FilerTimeout = TimeSpan.MinValue; // TimeSpan.FromSeconds(3);
        this.TimeoutUntilForcedRelease = TimeSpan.FromSeconds(10);
        this.DefaultSaveInterval = CrystalConfiguration.DefaultSaveInterval;
    }

    public bool EnableFilerLogger { get; set; } = false;

    public string DataDirectory { get; set; } = string.Empty;

    public TimeSpan FilerTimeout { get; set; }

    public long MemoryUsageLimit { get; set; } = DefaultMemoryUsageLimit;

    public int ConcurrentUnload { get; set; } = 8;

    public TimeSpan TimeoutUntilForcedRelease { get; set; }

    public SaveFormat DefaultSaveFormat { get; set; } = SaveFormat.Binary;

    public SavePolicy DefaultSavePolicy { get; set; } = SavePolicy.Manual;

    public TimeSpan DefaultSaveInterval { get; set; }

    public FileConfiguration? SupplementFile { get; set; }

    public FileConfiguration? BackupSupplementFile { get; set; }

    public DirectoryConfiguration GlobalDirectory { get; set; } = new LocalDirectoryConfiguration();

    public DirectoryConfiguration? DefaultBackup { get; set; }

    public StorageConfiguration GlobalStorage { get; set; } = new SimpleStorageConfiguration(new LocalDirectoryConfiguration("Storage"));
}
