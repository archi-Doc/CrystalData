// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Buffers.Text;

namespace CrystalData.Supplement;

public sealed partial class CrystalSupplement
{
    public const string DefaultSupplementFileName = "CrystalData.Supplement";
    public const string RipSuffix = ".Rip";
    private const int ItemLimit = 100;
    private static readonly SaveFormat Format = SaveFormat.Utf8;

    [TinyhandObject(LockObject = "lockObject")]
    private sealed partial class Data
    {
#pragma warning disable SA1401 // Fields should be private

        [TinyhandObject]
        [ValueLinkObject]
        private sealed partial class IdentifierItem
        {
            [Key(0)]
            [Link(Unique = true, Type = ChainType.Unordered)]
            public ulong Identifier;

            [Link(Name = "Timeline", Primary = true, Type = ChainType.QueueList)]
            public IdentifierItem()
            {
            }

            public IdentifierItem(ulong identifier)
            {
                this.Identifier = identifier;
            }
        }

        [TinyhandObject]
        [ValueLinkObject]
        private sealed partial class PlaneItem
        {
            [Key(0)]
            [Link(Unique = true, Type = ChainType.Unordered)]
            public uint Plane;

            [Key(1)]
            public Waypoint Waypoint0;

            [Key(2)]
            public ulong LeadingJournalPosition0;

            [Key(3)]
            public Waypoint Waypoint1;

            [Key(4)]
            public ulong LeadingJournalPosition1;

            [Key(5)]
            public Waypoint Waypoint2;

            [Key(6)]
            public ulong LeadingJournalPosition2;

            [Link(Name = "Timeline", Primary = true, Type = ChainType.QueueList)]
            public PlaneItem()
            {
            }

            public PlaneItem(uint plane)
            {
                this.Plane = plane;
            }
        }

#pragma warning restore SA1401 // Fields should be private

        #region FieldAndProperty

        private readonly Lock lockObject = new();

        [Key(0)]
        private IdentifierItem.GoshujinClass identifierItems = new();

        [Key(1)]
        private PlaneItem.GoshujinClass planeItems = new();

        #endregion

        public void ReportStored<TData>(FileConfiguration fileConfiguration)
        {
            var identifier = GetIdentifier<TData>(fileConfiguration);
            using (this.lockObject.EnterScope())
            {
                if (this.identifierItems.IdentifierChain.FindFirst(identifier) is { } item)
                {// Move to the end of the queue.
                    this.identifierItems.TimelineChain.Enqueue(item);
                }
                else
                {// New item.
                    this.identifierItems.Add(new(identifier));
                    while (this.identifierItems.Count > ItemLimit)
                    {// Remove the oldest item.
                        this.identifierItems.TimelineChain.Peek().Goshujin = default;
                    }
                }
            }
        }

        public bool IsPreviouslyStored<TData>(FileConfiguration fileConfiguration)
        {
            var identifier = GetIdentifier<TData>(fileConfiguration);
            using (this.lockObject.EnterScope())
            {
                return this.identifierItems.IdentifierChain.FindFirst(identifier) is not null;
            }
        }

        public ulong GetLeadingPosition(ref Waypoint waypoint)
        {
            var leadingJournalPosition = waypoint.JournalPosition;
            using (this.lockObject.EnterScope())
            {
                if (!this.planeItems.PlaneChain.TryGetValue(waypoint.Plane, out var planeItem))
                {
                    return leadingJournalPosition;
                }

                if (planeItem.Waypoint0.Equals(ref waypoint))
                {
                    if (leadingJournalPosition.CircularCompareTo(planeItem.LeadingJournalPosition0) < 0)
                    {
                        leadingJournalPosition = planeItem.LeadingJournalPosition0;
                    }
                }
                else if (planeItem.Waypoint1.Equals(ref waypoint))
                {
                    if (leadingJournalPosition.CircularCompareTo(planeItem.LeadingJournalPosition1) < 0)
                    {
                        leadingJournalPosition = planeItem.LeadingJournalPosition1;
                    }
                }
                else if (planeItem.Waypoint2.Equals(ref waypoint))
                {
                    if (leadingJournalPosition.CircularCompareTo(planeItem.LeadingJournalPosition2) < 0)
                    {
                        leadingJournalPosition = planeItem.LeadingJournalPosition2;
                    }
                }
            }

            return leadingJournalPosition;
        }

        public void SetLeadingPosition(ref Waypoint waypoint, ulong leadingJournalPosition)
        {
            leadingJournalPosition = Math.Max(leadingJournalPosition, waypoint.JournalPosition);

            using (this.lockObject.EnterScope())
            {
                if (this.planeItems.PlaneChain.TryGetValue(waypoint.Plane, out var item))
                {// Move to the end of the queue.
                    this.planeItems.TimelineChain.Enqueue(item);
                }
                else
                {// New item.
                    item = new(waypoint.Plane);
                    item.Goshujin = this.planeItems;
                    while (this.planeItems.Count > ItemLimit)
                    {// Remove the oldest item.
                        this.planeItems.TimelineChain.Peek().Goshujin = default;
                    }
                }

                if (item.Waypoint0.Equals(ref waypoint))
                {
                    if (leadingJournalPosition.CircularCompareTo(item.LeadingJournalPosition0) > 0)
                    {
                        item.LeadingJournalPosition0 = leadingJournalPosition;
                    }
                }
                else if (item.Waypoint1.Equals(ref waypoint))
                {
                    if (leadingJournalPosition.CircularCompareTo(item.LeadingJournalPosition1) > 0)
                    {
                        item.LeadingJournalPosition1 = leadingJournalPosition;
                    }
                }
                else if (item.Waypoint2.Equals(ref waypoint))
                {
                    if (leadingJournalPosition.CircularCompareTo(item.LeadingJournalPosition2) > 0)
                    {
                        item.LeadingJournalPosition2 = leadingJournalPosition;
                    }
                }
                else
                {// New waypoint.
                    if (waypoint.JournalPosition.CircularCompareTo(item.Waypoint0.JournalPosition) > 0)
                    {
                        item.Waypoint2 = item.Waypoint1;
                        item.LeadingJournalPosition2 = item.LeadingJournalPosition1;
                        item.Waypoint1 = item.Waypoint0;
                        item.LeadingJournalPosition1 = item.LeadingJournalPosition0;
                        item.Waypoint0 = waypoint;
                        item.LeadingJournalPosition0 = leadingJournalPosition;
                    }
                }
            }
        }
    }

    #region FieldAndProperty

    private readonly Crystalizer crystalizer;
    private readonly ILogger logger;
    private int ripCount;
    private Data data = new();
    private ISingleFiler? mainFiler;
    private ISingleFiler? backupFiler;
    private ISingleFiler? ripFiler;
    private FileConfiguration? mainConfiguration;
    private FileConfiguration? backupConfiguration;

    public bool IsRip => this.ripCount > 0;

    public bool IsSupplementLoaded { get; private set; }

    #endregion

    internal CrystalSupplement(Crystalizer crystalizer)
    {
        this.crystalizer = crystalizer;
        this.logger = this.crystalizer.UnitLogger.GetLogger<CrystalSupplement>();
    }

    public bool IsPreviouslyStored<TData>(FileConfiguration fileConfiguration)
        => this.data.IsPreviouslyStored<TData>(fileConfiguration);

    public void ReportStored<TData>(FileConfiguration fileConfiguration)
        => this.data.ReportStored<TData>(fileConfiguration);

    public ulong GetLeadingJournalPosition(ref Waypoint waypoint)
        => this.data.GetLeadingPosition(ref waypoint);

    public void SetLeadingJournalPosition(ref Waypoint waypoint, ulong leadingJournalPosition)
        => this.data.SetLeadingPosition(ref waypoint, leadingJournalPosition);

    internal void PrepareAndLoad()
    {
        if (this.mainFiler is null)
        {
            var fileConfiguration = this.crystalizer.Options.SupplementFile;
            fileConfiguration ??= new LocalFileConfiguration(DefaultSupplementFileName);
            (this.mainFiler, this.mainConfiguration) = this.crystalizer.ResolveAndPrepareAndCheckSingleFiler<CrystalSupplement>(fileConfiguration).Result;
        }

        if (this.backupFiler is null && this.crystalizer.Options.BackupSupplementFile is not null)
        {
            (this.backupFiler, this.backupConfiguration) = this.crystalizer.ResolveAndPrepareAndCheckSingleFiler<CrystalSupplement>(this.crystalizer.Options.BackupSupplementFile).Result;
        }

        if (this.ripFiler is null && this.mainConfiguration is not null)
        {
            var configuration = this.mainConfiguration.AppendPath(RipSuffix);
            (this.ripFiler, _) = this.crystalizer.ResolveAndPrepareAndCheckSingleFiler<CrystalSupplement>(configuration).Result;

            if (this.ripFiler is not null)
            {// Load rip file
                var ripResult = this.ripFiler.ReadAsync(0, -1).Result;
                if (ripResult.IsSuccess)
                {
                    if (Utf8Parser.TryParse(ripResult.Data.Span, out int ripCount, out _))
                    {
                        this.ripCount = ripCount;
                    }

                    ripResult.Return();
                    this.ripFiler.DeleteAndForget();
                }
            }
        }

        if (this.mainFiler is not null &&
            LoadSupplementFile(this.mainFiler, this.mainConfiguration?.Path ?? string.Empty))
        {
            return;
        }

        if (this.backupFiler is not null &&
            LoadSupplementFile(this.backupFiler, this.backupConfiguration?.Path ?? string.Empty))
        {
            return;
        }

        bool LoadSupplementFile(ISingleFiler filer, string? path)
        {
            var pathAndRip = $"'{path}' ({this.ripCount})";
            var fileResult = filer.ReadAsync(0, -1).Result;
            try
            {
                var deserializeResult = SerializeHelper.TryDeserialize<Data>(fileResult.Data.Span, Format, false, default);
                if (deserializeResult.Data is not null)
                {
                    this.data = deserializeResult.Data;
                    this.IsSupplementLoaded = true;
                    this.logger.TryGet()?.Log(CrystalDataHashed.CrystalSupplement.LoadSuccess, pathAndRip);
                    return true;
                }
            }
            finally
            {
                fileResult.Return();
            }

            this.logger.TryGet()?.Log(CrystalDataHashed.CrystalSupplement.LoadFailure, pathAndRip);
            return false;
        }
    }

    internal async Task Store(bool rip)
    {
        if (rip)
        {
            this.ripCount++;
            if (this.ripFiler is not null)
            {
                var rent = BytePool.Default.Rent(32);
                Utf8Formatter.TryFormat(this.ripCount, rent.AsSpan(), out var written);
                await this.ripFiler.WriteAsync(0, rent.AsReadOnly(0, written)).ConfigureAwait(false);
                rent.Return();
            }
        }

        BytePool.RentMemory rentMemory = default;
        try
        {
            if (Format == SaveFormat.Binary)
            {
                rentMemory = TinyhandSerializer.SerializeObjectToRentMemory(this.data);
            }
            else
            {
                rentMemory = TinyhandSerializer.SerializeObjectToUtf8RentMemory(this.data);
            }

            if (this.mainFiler is not null)
            {
                await this.mainFiler.WriteAsync(0, rentMemory.ReadOnly).ConfigureAwait(false);
            }

            if (this.backupFiler is not null)
            {
                await this.backupFiler.WriteAsync(0, rentMemory.ReadOnly).ConfigureAwait(false);
            }
        }
        catch
        {
        }
        finally
        {
            rentMemory.Return();
        }
    }

    private static ulong GetIdentifier<TData>(FileConfiguration fileConfiguration)
        => XxHash3Slim.Hash64(typeof(TData).FullName ?? string.Empty) ^ XxHash3Slim.Hash64(fileConfiguration.Path);
}
