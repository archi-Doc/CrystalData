// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Tinyhand.IO;

namespace CrystalData.Supplement;

public sealed partial class CrystalSupplement
{
    public const string DefaultSupplementFileName = "CrystalData.Supplement";
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

        public bool IsPreviouslyStored<TData>(FileConfiguration fileConfiguration)
        {
            var identifier = GetIdentifier<TData>(fileConfiguration);
            using (this.lockObject.EnterScope())
            {
                var result = this.previouslyStoredIdentifiers.Contains(identifier);
                return result;
            }
        }
    }

    private readonly Crystalizer crystalizer;
    private readonly ILogger logger;
    private Data data = new();
    private IFiler? mainFiler;
    private IFiler? backupFiler;
    private FileConfiguration? mainConfiguration;
    private FileConfiguration? backupConfiguration;

    public CrystalSupplement(Crystalizer crystalizer)
    {
        this.crystalizer = crystalizer;
        this.logger = this.crystalizer.UnitLogger.GetLogger<CrystalSupplement>();
    }

    public void PrepareAndLoad()
    {
        var supplementLoaded = false;

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

        var result = this.mainFiler.ReadAsync(0, -1).Result;
        if (result.IsSuccess)
        {
            var reader = new TinyhandReader(result.Data.Span);
            if (TinyhandSerializer.TryDeserializeObject<Data>(result.Data.Span, out var d))
            {
                this.data = d;
                supplementLoaded = true;
                this.logger.TryGet()?.Log(CrystalDataHashed.CrystalSupplement.LoadSuccess, this.mainConfiguration?.Path ?? string.Empty);
            }

            result.Return();
        }

        if (supplementLoaded)
        {
            return;
        }

        this.logger.TryGet()?.Log(CrystalDataHashed.CrystalSupplement.LoadFailure, this.mainConfiguration?.Path ?? string.Empty);

    }

    public void Store(bool rip = false)
    {
        var options = Format == SaveFormat.Binary ? TinyhandSerializerOptions.Standard : TinyhandSerializerOptions.ConvertToString;
        BytePool.RentMemory rentMemory = default;
        try
        {
            rentMemory = TinyhandSerializer.SerializeObjectToRentMemory(this.data, options);

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

    private static ulong GetIdentifier<TData>(FileConfiguration fileConfiguration)
        => XxHash3Slim.Hash64(typeof(TData).FullName ?? string.Empty) ^ XxHash3Slim.Hash64(fileConfiguration.Path);
}
