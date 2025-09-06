// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Buffers.Text;

namespace CrystalData.Supplement;

public sealed partial class CrystalSupplement
{
    public const string DefaultSupplementFileName = "CrystalData.Supplement";
    public const string RipSuffix = ".Rip";
    private const int PreviouslyStoredLimit = 1_000;
    private static readonly SaveFormat Format = SaveFormat.Utf8;

    [TinyhandObject(LockObject = "lockObject")]
    private sealed partial class Data
    {
        [TinyhandObject]
        [ValueLinkObject]
        private sealed partial class PlaneItem
        {
            public PlaneItem()
            {
            }

            [Key(0)]
            [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
            public uint Plane { get; private set; }
        }

        #region FieldAndProperty

        private readonly Lock lockObject = new();

        [Key(0)]
        private readonly HashSet<ulong> previouslyStoredIdentifiers = new();

        [Key(1)]
        private readonly PlaneItem.GoshujinClass planeItems = new();

        #endregion

        public void ReportStored<TData>(FileConfiguration fileConfiguration)
        {
            var identifier = GetIdentifier<TData>(fileConfiguration);
            using (this.lockObject.EnterScope())
            {
                this.previouslyStoredIdentifiers.Add(identifier);
            }
        }

        public bool IsPreviouslyStored<TData>(FileConfiguration fileConfiguration)
        {
            var identifier = GetIdentifier<TData>(fileConfiguration);
            using (this.lockObject.EnterScope())
            {
                var result = this.previouslyStoredIdentifiers.Contains(identifier);
                return result;
            }
        }

        public void AfterLoad()
        {
            using (this.lockObject.EnterScope())
            {
            }
        }

        public void OnSaving()
        {
            using (this.lockObject.EnterScope())
            {
                while (this.previouslyStoredIdentifiers.Count > PreviouslyStoredLimit)
                {
                    var item = this.previouslyStoredIdentifiers.First();
                    this.previouslyStoredIdentifiers.Remove(item);
                }
            }
        }

        public ulong GetLeadingPosition(ref Waypoint waypoint)
        {
            throw new NotImplementedException();
        }

        public void SetLeadingPosition(ref Waypoint waypoint, ulong leadingJournalPosition)
        {
            leadingJournalPosition = Math.Max(leadingJournalPosition, waypoint.JournalPosition);
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
                    this.data.AfterLoad();
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
        this.data.OnSaving();

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
