// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Supplement;

public sealed partial class CrystalSupplement
{
    public const string DefaultSupplementFileName = "CrystalData.Supplement";
    private const int PreviouslyStoredLimit = 1_000;
    private static readonly SaveFormat Format = SaveFormat.Utf8;

    [TinyhandObject(LockObject = "lockObject")]
    private sealed partial class Data
    {
        #region FieldAndProperty

        private readonly Lock lockObject = new();

        [Key(0)]
        public bool IsRip { get; private set; }

        [Key(1)]
        private readonly HashSet<ulong> previouslyStoredIdentifiers = new();

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

            if (!this.IsRip)
            {// Previously not rip.
             // this.logger.TryGet()?.Log(CrystalDataHashed.CrystalSupplement.IsRip);
            }
        }

        public void OnSaving(bool isRip)
        {
            using (this.lockObject.EnterScope())
            {
                this.IsRip = isRip;

                while (this.previouslyStoredIdentifiers.Count > PreviouslyStoredLimit)
                {
                    var item = this.previouslyStoredIdentifiers.First();
                    this.previouslyStoredIdentifiers.Remove(item);
                }
            }
        }
    }

    #region FieldAndProperty

    private readonly Crystalizer crystalizer;
    private readonly ILogger logger;
    private Data data = new();
    private IFiler? mainFiler;
    private IFiler? backupFiler;
    private FileConfiguration? mainConfiguration;
    private FileConfiguration? backupConfiguration;

    public bool IsSupplementLoaded { get; private set; }

    #endregion

    public CrystalSupplement(Crystalizer crystalizer)
    {
        this.crystalizer = crystalizer;
        this.logger = this.crystalizer.UnitLogger.GetLogger<CrystalSupplement>();
    }

    public void PrepareAndLoad()
    {
        if (this.mainFiler is null)
        {
            var fileConfiguration = this.crystalizer.Options.SupplementFile;
            fileConfiguration ??= new LocalFileConfiguration(DefaultSupplementFileName);
            (this.mainFiler, this.mainConfiguration) = this.crystalizer.ResolveFiler(fileConfiguration);
        }

        if (this.backupFiler is null && this.crystalizer.Options.BackupSupplementFile is not null)
        {
            (this.backupFiler, this.backupConfiguration) = this.crystalizer.ResolveFiler(this.crystalizer.Options.BackupSupplementFile);
        }

        if (LoadSupplementFile(this.mainFiler, this.mainConfiguration?.Path ?? string.Empty))
        {
            return;
        }

        if (this.backupFiler is not null &&
            LoadSupplementFile(this.backupFiler, this.backupConfiguration?.Path ?? string.Empty))
        {
            return;
        }

        bool LoadSupplementFile(IFiler filer, string? path)
        {
            var fileResult = filer.ReadAsync(0, -1).Result;
            try
            {
                var deserializeResult = SerializeHelper.TryDeserialize<Data>(fileResult.Data.Span, Format, false, default);
                if (deserializeResult.Data is not null)
                {
                    this.data = deserializeResult.Data;
                    this.IsSupplementLoaded = true;
                    this.logger.TryGet()?.Log(CrystalDataHashed.CrystalSupplement.LoadSuccess, path ?? string.Empty);
                    this.data.AfterLoad();
                    return true;
                }
            }
            finally
            {
                fileResult.Return();
            }

            this.logger.TryGet()?.Log(CrystalDataHashed.CrystalSupplement.LoadFailure, path ?? string.Empty);
            return false;
        }
    }

    public void Store(bool isRip)
    {
        this.data.OnSaving(isRip);

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
                this.mainFiler.WriteAsync(0, rentMemory.ReadOnly).Wait();
            }

            if (this.backupFiler is not null)
            {
                this.backupFiler.WriteAsync(0, rentMemory.ReadOnly).Wait();
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

    public bool IsPreviouslyStored<TData>(FileConfiguration fileConfiguration)
        => this.data.IsPreviouslyStored<TData>(fileConfiguration);

    public void ReportStored<TData>(FileConfiguration fileConfiguration)
        => this.data.ReportStored<TData>(fileConfiguration);

    private static ulong GetIdentifier<TData>(FileConfiguration fileConfiguration)
        => XxHash3Slim.Hash64(typeof(TData).FullName ?? string.Empty) ^ XxHash3Slim.Hash64(fileConfiguration.Path);
}
