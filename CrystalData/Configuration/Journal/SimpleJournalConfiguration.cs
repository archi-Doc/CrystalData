// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

[TinyhandObject]
public partial record SimpleJournalConfiguration : JournalConfiguration
{
    public const int DefaultJournalCapacityInMBs = 256; // 256 MB
    public const int DefaultSaveIntervalInMilliseconds = 1_000;

    public SimpleJournalConfiguration()
        : this(EmptyDirectoryConfiguration.Default, 0)
    {
    }

    public SimpleJournalConfiguration(DirectoryConfiguration configuration, int journalCapacityInMBs = DefaultJournalCapacityInMBs, int saveIntervalInMilliseconds = DefaultSaveIntervalInMilliseconds)
        : base()
    {
        this.DirectoryConfiguration = configuration;

        this.JournalCapacityInMBs = journalCapacityInMBs;
        if (this.JournalCapacityInMBs < DefaultJournalCapacityInMBs)
        {
            this.JournalCapacityInMBs = DefaultJournalCapacityInMBs;
        }

        this.SaveIntervalInMilliseconds = saveIntervalInMilliseconds;
        if (this.SaveIntervalInMilliseconds < DefaultSaveIntervalInMilliseconds)
        {
            this.SaveIntervalInMilliseconds = DefaultSaveIntervalInMilliseconds;
        }
    }

    [MemberNameAsKey]
    public DirectoryConfiguration DirectoryConfiguration { get; protected set; }

    [MemberNameAsKey]
    public DirectoryConfiguration? BackupDirectoryConfiguration { get; init; }

    [MemberNameAsKey]
    public int JournalCapacityInMBs { get; protected set; }

    [MemberNameAsKey]
    public int SaveIntervalInMilliseconds { get; protected set; }

    [IgnoreMember]
    public int MaxRecordLength { get; protected set; } = 1024 * 16; // 16 KB

    [IgnoreMember]
    public int CompleteBookLength { get; protected set; } = 1024 * 1024 * 16; // 16 MB

    [IgnoreMember]
    public int MaxMemoryCapacity { get; protected set; } = 1024 * 1024 * 64; // 64 MB
}
