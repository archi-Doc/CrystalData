// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CrystalData.Filer;
using CrystalData.Storage;
using Microsoft.Extensions.DependencyInjection;
using Tinyhand.IO;

namespace CrystalData;

public sealed class CrystalObject<TData> : ICrystalInternal<TData>, IStructualObject
    where TData : class, ITinyhandSerializable<TData>, ITinyhandReconstructable<TData>
{// Data + Journal/Waypoint + Filer/FileConfiguration + Storage/StorageConfiguration
    internal CrystalObject(Crystalizer crystalizer)
    {
        this.Crystalizer = crystalizer;
        this.originalCrystalConfiguration = CrystalConfiguration.Default;
        this.crystalConfiguration = CrystalConfiguration.Default;
        ((IStructualObject)this).StructualRoot = this;
    }

    #region FieldAndProperty

    private SemaphoreLock semaphore = new();
    private TData? data;
    private CrystalFiler? crystalFiler;
    private IStorage? storage;
    private Waypoint waypoint;
    private DateTime lastSavedTime;
    private CrystalConfiguration originalCrystalConfiguration;
    private CrystalConfiguration crystalConfiguration;

    #endregion

    #region ICrystal

    public Crystalizer Crystalizer { get; }

    public CrystalConfiguration OriginalCrystalConfiguration => this.originalCrystalConfiguration;

    public CrystalConfiguration CrystalConfiguration => this.crystalConfiguration;

    public Type DataType => typeof(TData);

    object ICrystal.Data => ((ICrystal<TData>)this).Data!;

    public TData Data
    {
        get
        {
            if (this.data is { } v)
            {
                return v;
            }

            using (this.semaphore.EnterScope())
            {
                if (this.State == CrystalState.Initial)
                {// Initial
                    this.PrepareAndLoadInternal(false).Wait();
                }
                else if (this.State == CrystalState.Deleted)
                {// Deleted
                    TinyhandSerializer.ReconstructObject<TData>(ref this.data);
                    this.SetupData();
                    return this.data;
                }

                if (this.data is not null)
                {
                    return this.data;
                }

                // Finally, reconstruct
                this.ResetWaypoint(true);
                return this.data;
            }
        }
    }

    public CrystalState State { get; private set; }

    public IStorage Storage
    {
        get
        {
            if (this.storage is { } v)
            {
                return v;
            }

            using (this.semaphore.EnterScope())
            {
                if (this.storage != null)
                {
                    return this.storage;
                }

                this.ResolveAndPrepareStorage();
                return this.storage;
            }
        }
    }

    public IJournal? Journal => this.Crystalizer.Journal;

    void ICrystal.Configure(CrystalConfiguration configuration)
    {
        using (this.semaphore.EnterScope())
        {
            this.originalCrystalConfiguration = configuration;
            this.PrepareCrystalConfiguration();
            this.crystalFiler = null;
            this.storage = null;
            this.State = CrystalState.Initial;
        }
    }

    void ICrystal.ConfigureFile(FileConfiguration configuration)
    {
        using (this.semaphore.EnterScope())
        {
            this.originalCrystalConfiguration = this.originalCrystalConfiguration with { FileConfiguration = configuration, };
            this.PrepareCrystalConfiguration();
            this.crystalFiler = null;
            this.State = CrystalState.Initial;
        }
    }

    void ICrystal.ConfigureStorage(StorageConfiguration configuration)
    {
        using (this.semaphore.EnterScope())
        {
            this.originalCrystalConfiguration = this.originalCrystalConfiguration with { StorageConfiguration = configuration, };
            this.PrepareCrystalConfiguration();
            this.storage = null;
            this.State = CrystalState.Initial;
        }
    }

    async Task<CrystalResult> ICrystal.PrepareAndLoad(bool useQuery)
    {
        using (this.semaphore.EnterScope())
        {
            if (this.State == CrystalState.Prepared)
            {// Prepared
                return CrystalResult.Success;
            }
            else if (this.State == CrystalState.Deleted)
            {// Deleted
                return CrystalResult.Deleted;
            }

            return await this.PrepareAndLoadInternal(useQuery).ConfigureAwait(false);
        }
    }

    async Task<CrystalResult> IPersistable.Store(StoreMode storeMode, CancellationToken cancellationToken)
    {
        if (this.CrystalConfiguration.SavePolicy == SavePolicy.Volatile)
        {// Volatile
            if (storeMode != StoreMode.StoreOnly)
            {// Release
                using (this.semaphore.EnterScope())
                {
                    this.data = null;
                    this.State = CrystalState.Initial;
                }
            }

            return CrystalResult.Success;
        }

        var obj = Volatile.Read(ref this.data);
        var filer = Volatile.Read(ref this.crystalFiler);
        var currentWaypoint = this.waypoint;

        if (this.State == CrystalState.Initial)
        {// Initial
            return CrystalResult.NotPrepared;
        }
        else if (this.State == CrystalState.Deleted)
        {// Deleted
            return CrystalResult.Deleted;
        }
        else if (obj == null || filer == null)
        {
            return CrystalResult.NotPrepared;
        }

        var semaphore = obj as IRepeatableReadSemaphore;
        if (semaphore is not null)
        {
            if (storeMode == StoreMode.TryRelease)
            {
                semaphore.LockAndTryRelease(out var state);
                if (state == GoshujinState.Valid)
                {// Cannot unload because a WriterClass is still present.
                    return CrystalResult.DataIsLocked;
                }
                else if (state == GoshujinState.Releasing)
                {// Unload (Success)
                    if (semaphore.SemaphoreCount > 0)
                    {
                        return CrystalResult.DataIsLocked;
                    }
                }
                else
                {// Obsolete
                    return CrystalResult.DataIsObsolete;
                }
            }
            else if (storeMode == StoreMode.ForceRelease)
            {
                semaphore.LockAndForceRelease();
            }
        }

        this.lastSavedTime = DateTime.UtcNow;

        // Starting position
        var startingPosition = this.Crystalizer.GetJournalPosition();

        // Serialize
        BytePool.RentMemory rentMemory;
        try
        {
            if (this.CrystalConfiguration.SaveFormat == SaveFormat.Utf8)
            {// utf8
                rentMemory = TinyhandSerializer.SerializeObjectToUtf8RentMemory(obj);
            }
            else
            {// binary
                rentMemory = TinyhandSerializer.SerializeObjectToRentMemory(obj);
            }
        }
        catch
        {
            return CrystalResult.SerializationFailed;
        }

        if (obj is IStructualObject structualObject)
        {// Since data may be released by StoreData(), this should be invoked only after serialization.
            if (await structualObject.StoreData(storeMode).ConfigureAwait(false) == false)
            {
                return CrystalResult.DataIsLocked;
            }
        }

        /*if (this.storage is { } storage && storage is not EmptyStorage)
        {// Because multiple Crystals may share a single Storage, saving the Storage is handled separately.
            await storage.SaveStorage(this).ConfigureAwait(false);
        }*/

        // Get hash
        var hash = FarmHash.Hash64(rentMemory.Span);
        if (hash == currentWaypoint.Hash)
        {// Identical data
            goto Exit;
        }

        var waypoint = this.waypoint;
        if (!waypoint.Equals(currentWaypoint))
        {// Waypoint changed
            Debug.Assert(false, "Waypoint changed during Store.");
            goto Exit;
        }

        this.Crystalizer.UpdateWaypoint(this, ref currentWaypoint, hash);

        var result = await filer.Save(rentMemory.ReadOnly, currentWaypoint).ConfigureAwait(false);
        if (result != CrystalResult.Success)
        {// Write error
            return result;
        }

        this.Crystalizer.CrystalSupplement.ReportStored<TData>(this.CrystalConfiguration.FileConfiguration);
        using (this.semaphore.EnterScope())
        {// Update waypoint and plane position.
            this.waypoint = currentWaypoint;
            this.Crystalizer.CrystalCheck.SetShortcutPosition(currentWaypoint, startingPosition);
            if (storeMode != StoreMode.StoreOnly)
            {// Unload
                this.data = null;
                this.State = CrystalState.Initial;
            }
        }

        _ = filer.LimitNumberOfFiles();
        return CrystalResult.Success;

Exit:
        this.Crystalizer.CrystalSupplement.ReportStored<TData>(this.CrystalConfiguration.FileConfiguration);
        using (this.semaphore.EnterScope())
        {
            this.Crystalizer.CrystalCheck.SetShortcutPosition(currentWaypoint, startingPosition);
            if (storeMode != StoreMode.StoreOnly)
            {// Unload
                this.data = null;
                this.State = CrystalState.Initial;
            }
        }

        return CrystalResult.Success;
    }

    async Task<CrystalResult> ICrystal.Delete()
    {
        using (this.semaphore.EnterScope())
        {
            if (this.State == CrystalState.Initial)
            {// Initial
                await this.PrepareAndLoadInternal(false).ConfigureAwait(false);
            }
            else if (this.State == CrystalState.Deleted)
            {// Deleted
                return CrystalResult.Success;
            }

            // Delete file
            this.ResolveAndPrepareFiler();
            await this.crystalFiler.DeleteAllAsync().ConfigureAwait(false);

            // Delete storage
            if (this.CrystalConfiguration.StorageConfiguration != EmptyStorageConfiguration.Default)
            {// StorageMap uses Storage internally, and this prevents infinite recursive calls caused by it.
                this.ResolveAndPrepareStorage();
                await this.storage.DeleteStorageAsync().ConfigureAwait(false);
            }

            // Journal/Waypoint
            this.Crystalizer.RemovePlane(this.waypoint);
            this.waypoint = default;

            // Clear
            TinyhandSerializer.DeserializeObject(TinyhandSerializer.SerializeObject(TinyhandSerializer.ReconstructObject<TData>()), ref this.data);
            this.SetupData();
            // this.obj = default;
            // TinyhandSerializer.ReconstructObject<TData>(ref this.obj);

            this.State = CrystalState.Deleted;
        }

        this.Crystalizer.DeleteInternal(this);
        return CrystalResult.Success;
    }

    Task? ICrystalInternal.TryPeriodicStore(DateTime utc)
    {
        if (this.CrystalConfiguration.SavePolicy != SavePolicy.Periodic)
        {
            return null;
        }

        var elapsed = utc - this.lastSavedTime;
        if (elapsed < this.CrystalConfiguration.SaveInterval)
        {
            return null;
        }

        this.lastSavedTime = utc;
        return ((ICrystal)this).Store(StoreMode.StoreOnly);
    }

    Waypoint ICrystalInternal.Waypoint
        => this.waypoint;

    IStructualRoot? IStructualObject.StructualRoot { get; set; }

    IStructualObject? IStructualObject.StructualParent { get; set; } = null;

    int IStructualObject.StructualKey { get; set; } = -1;

    /*void IStructualObject.WriteLocator(ref Tinyhand.IO.TinyhandWriter writer)
    {
        writer.Write_Locator();
        writer.Write(this.waypoint.Plane);
    }*/

    async Task<bool> IPersistable.TestJournal()
    {
        if (this.Crystalizer.Journal is not CrystalData.Journal.SimpleJournal journal)
        {// No journaling
            return true;
        }

        var testResult = true;
        using (this.semaphore.EnterScope())
        {
            if (this.crystalFiler is null ||
                this.crystalFiler.Main is not { } main)
            {
                return testResult;
            }

            var waypoints = main.GetWaypoints();
            if (waypoints.Length <= 1)
            {// The number of waypoints is 1 or less.
                return testResult;
            }

            var logger = this.Crystalizer.UnitLogger.GetLogger<TData>();
            TData? previousObject = default;
            for (var i = 0; i < waypoints.Length; i++)
            {// waypoint[i] -> waypoint[i + 1]
                var base32 = waypoints[i].ToBase32();

                // Load
                var result = await main.LoadWaypoint(waypoints[i]).ConfigureAwait(false);
                if (result.IsFailure)
                {// Loading error
                    logger.TryGet(LogLevel.Error)?.Log(CrystalDataHashed.TestJournal.LoadingFailure, base32);
                    testResult = false;
                    break;
                }

                // Deserialize
                (var currentObject, var currentFormat) = SerializeHelper.TryDeserialize<TData>(result.Data.Span, this.CrystalConfiguration.SaveFormat, true, default);
                if (currentObject is null)
                {// Deserialization error
                    result.Return();
                    logger.TryGet(LogLevel.Error)?.Log(CrystalDataHashed.TestJournal.DeserializationFailure, base32);
                    testResult = false;
                    break;
                }

                if (currentObject is IStructualObject structualObject)
                {
                    structualObject.SetupStructure(this);
                }

                if (previousObject is not null)
                {// Compare the previous data
                    bool compare;
                    if (currentFormat == SaveFormat.Binary)
                    {// Previous (previousObject), Current (currentObject/result.Data.Span): Binary
                        compare = result.Data.Span.SequenceEqual(TinyhandSerializer.Serialize(previousObject));
                    }
                    else
                    {// Previous (previousObject), Current (currentObject/result.Data.Span): Utf8
                        compare = result.Data.Span.SequenceEqual(TinyhandSerializer.SerializeToUtf8(previousObject));
                    }

                    if (compare)
                    {// Success
                        logger.TryGet(LogLevel.Information)?.Log(CrystalDataHashed.TestJournal.Success, base32);
                    }
                    else
                    {// Failure
                        logger.TryGet(LogLevel.Error)?.Log(CrystalDataHashed.TestJournal.Failure, base32);
                        testResult = false;
                    }
                }

                result.Return();

                if (currentObject is not IStructualObject journalObject)
                {
                    break;
                }
                else if (i == waypoints.Length - 1)
                {
                    break;
                }

                // Read journal [waypoints[i].JournalPosition, waypoints[i + 1].JournalPosition)
                var length = (int)(waypoints[i + 1].JournalPosition - waypoints[i].JournalPosition);
                var memoryOwner = BytePool.Default.Rent(length).AsMemory(0, length);
                var journalResult = await journal.ReadJournalAsync(waypoints[i].JournalPosition, waypoints[i + 1].JournalPosition, memoryOwner.Memory).ConfigureAwait(false);
                if (!journalResult)
                {// Journal error
                    testResult = false;
                    break;
                }

                this.ReadJournal(journalObject, memoryOwner.Memory, waypoints[i].Plane);

                previousObject = currentObject;
            }
        }

        return testResult;
    }

    void ICrystalInternal.SetStorage(IStorage storage)
    {
        using (this.semaphore.EnterScope())
        {
            this.storage = storage;
        }
    }

    #endregion

    #region Structual

    bool IStructualRoot.TryGetJournalWriter(JournalType recordType, out TinyhandWriter writer)
    {
        if (this.Crystalizer.Journal is not null)
        {
            this.Crystalizer.Journal.GetWriter(recordType, out writer);

            writer.Write_Locator();
            writer.Write(this.waypoint.Plane);
            return true;
        }
        else
        {
            writer = default;
            return false;
        }
    }

    ulong IStructualRoot.AddJournal(ref TinyhandWriter writer)
    {
        if (this.Crystalizer.Journal is not null)
        {
            return this.Crystalizer.Journal.Add(ref writer);
        }
        else
        {
            return 0;
        }
    }

    bool IStructualRoot.TryAddToSaveQueue()
    {
        if (this.CrystalConfiguration.SavePolicy == SavePolicy.OnChanged)
        {
            this.Crystalizer.AddToSaveQueue(this);
            return true;
        }
        else
        {
            return false;
        }
    }

    /*ulong ICrystal.AddStartingPoint()
    {
        if (this.Crystalizer.Journal is not null)
        {
            return this.Crystalizer.Journal.AddStartingPoint();
        }
        else
        {
            return 0;
        }
    }*/

    #endregion

    private bool ReadJournal(IStructualObject journalObject, ReadOnlyMemory<byte> data, uint currentPlane)
    {
        var reader = new TinyhandReader(data.Span);
        var success = true;

        while (reader.Consumed < data.Length)
        {
            if (!reader.TryReadRecord(out var length, out var journalType))
            {
                return false;
            }

            var fork = reader.Fork();
            try
            {
                if (journalType == JournalType.Record)
                {
                    reader.Read_Locator();
                    var plane = reader.ReadUInt32();

                    if (plane == currentPlane)
                    {
                        if (journalObject.ReadRecord(ref reader))
                        {// Success
                        }
                        else
                        {// Failure
                            success = false;
                        }
                    }
                }
                else
                {
                }
            }
            catch
            {
                success = false;
            }
            finally
            {
                reader = fork;
                reader.Advance(length);
            }
        }

        return success;
    }

    private async Task<CrystalResult> PrepareAndLoadInternal(bool useQuery)
    {// this.semaphore.EnterScope()
        CrystalResult result;
        var param = PrepareParam.New<TData>(this.Crystalizer, useQuery);

        // CrystalFiler
        if (this.crystalFiler == null)
        {
            this.crystalFiler = new(this.Crystalizer);
            result = await this.crystalFiler.PrepareAndCheck(param, this.CrystalConfiguration).ConfigureAwait(false);
            if (result.IsFailure())
            {
                return result;
            }
        }

        // Storage
        if (this.storage == null)
        {
            var storageConfiguration = this.CrystalConfiguration.StorageConfiguration;
            this.storage = this.Crystalizer.ResolveStorage(ref storageConfiguration);
            result = await this.storage.PrepareAndCheck(param, storageConfiguration).ConfigureAwait(false);
            if (result.IsFailure())
            {
                return result;
            }
        }

        // Data
        if (this.data is not null)
        {
            this.State = CrystalState.Prepared;
            return CrystalResult.Success;
        }

        var singletonData = this.data;
        if (singletonData is null &&
            this.originalCrystalConfiguration.IsSingleton)
        {// For singleton data, it is always treated as a singleton instance, regardless of whether or not ServiceProvider is used.
            singletonData = this.Crystalizer.ServiceProvider.GetRequiredService<TData>();
        }

        var filer = Volatile.Read(ref this.crystalFiler);
        var configuration = this.CrystalConfiguration;

        // !!! EXIT !!!
        this.semaphore.Exit();
        (CrystalResult Result, TData? Data, Waypoint Waypoint) loadResult;
        try
        {
            loadResult = await LoadAndDeserializeNotInternal(filer, param, configuration, singletonData).ConfigureAwait(false);
        }
        finally
        {
            this.semaphore.Enter();
        }

        // !!! ENTERED !!!
        if (this.data is not null)
        {
            return CrystalResult.Success;
        }
        else if (loadResult.Result.IsFailure())
        {
            return loadResult.Result;
        }

        // Check journal position
        if (loadResult.Waypoint.IsValid && this.Crystalizer.Journal is { } journal)
        {
            if (loadResult.Waypoint.JournalPosition.CircularCompareTo(journal.GetCurrentPosition()) > 0)
            {
                var query = await param.Query.InconsistentJournal(this.CrystalConfiguration.FileConfiguration.Path).ConfigureAwait(false);
                if (query == AbortOrContinue.Abort)
                {
                    return CrystalResult.CorruptedData;
                }
                else
                {
                    journal.ResetJournal(loadResult.Waypoint.JournalPosition);
                }
            }
        }

        if (loadResult.Data is { } data)
        {// Loaded
            this.data = data;
            this.waypoint = loadResult.Waypoint;
            if (this.CrystalConfiguration.HasFileHistories)
            {
                if (this.waypoint.IsValid)
                {// Valid waypoint
                    this.Crystalizer.SetPlane(this, ref this.waypoint);
                }
                else
                {// Invalid waypoint
                    this.ResetWaypoint(false);
                }
            }

            // this.LogWaypoint("Load");
            this.SetupData();
            this.State = CrystalState.Prepared;
            return CrystalResult.Success;
        }
        else
        {// Reconstruct
            this.data = singletonData;
            this.ResetWaypoint(true);

            // this.LogWaypoint("Reconstruct");
            this.SetupData();
            this.State = CrystalState.Prepared;
            return CrystalResult.Success;
        }
    }

#pragma warning disable SA1204 // Static elements should appear before instance elements
    private static async Task<(CrystalResult Result, TData? Data, Waypoint Waypoint)> LoadAndDeserializeNotInternal(CrystalFiler filer, PrepareParam param, CrystalConfiguration configuration, TData? singletonData)
#pragma warning restore SA1204 // Static elements should appear before instance elements
    {
        var isPreviouslyStored = filer.Crystalizer.CrystalSupplement.IsPreviouslyStored<TData>(configuration.FileConfiguration);
        // param.RegisterConfiguration(configuration.FileConfiguration, out var newlyRegistered);

        // Load data (the hash is checked by CrystalFiler)
        var data = await filer.LoadLatest<TData>(param, configuration.SaveFormat, singletonData).ConfigureAwait(false);
        if (data.Result.IsFailure)
        {
            if (isPreviouslyStored &&
                configuration.RequiredForLoading &&
                await param.Query.FailedToLoad(configuration.FileConfiguration, data.Result.Result).ConfigureAwait(false) == AbortOrContinue.Abort)
            {
                return (data.Result.Result, default, default);
            }

            return (CrystalResult.Success, default, default); // Reconstruct
        }

        /*if (configuration.RequiredForLoading &&
            await param.Query.FailedToLoad(configuration.FileConfiguration, CrystalResult.DeserializationFailed).ConfigureAwait(false) == AbortOrContinue.Abort)
        {
            return (data.Result.Result, default, default);
        }*/

        if (configuration.HasFileHistories)
        {
            return (CrystalResult.Success, data.Result.Object, data.Waypoint);
        }
        else
        {// Calculate a hash to prevent saving the same data.
            // var waypoint = data.Waypoint.WithHash(FarmHash.Hash64(data.Result.Data.Memory.Span));//
            var waypoint = data.Waypoint.WithHash(data.Waypoint.Hash);
            return (CrystalResult.Success, data.Result.Object, waypoint);
        }
    }

    [MemberNotNull(nameof(crystalFiler))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResolveAndPrepareFiler()
    {
        if (this.crystalFiler == null)
        {
            this.crystalFiler = new(this.Crystalizer);
            this.crystalFiler.PrepareAndCheck(PrepareParam.NoQuery<TData>(this.Crystalizer), this.CrystalConfiguration).Wait();
        }
    }

    [MemberNotNull(nameof(storage))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResolveAndPrepareStorage()
    {
        if (this.storage == null)
        {
            var storageConfiguration = this.CrystalConfiguration.StorageConfiguration;
            this.storage = this.Crystalizer.ResolveStorage(ref storageConfiguration);
            this.storage.PrepareAndCheck(PrepareParam.NoQuery<TData>(this.Crystalizer), storageConfiguration).Wait();
        }
    }

    [MemberNotNull(nameof(data))]
    private void ResetWaypoint(bool reconstructObject)
    {
        if (reconstructObject || this.data is null)
        {
            TinyhandSerializer.ReconstructObject<TData>(ref this.data);
        }

        BytePool.RentMemory rentMemory = default;
        try
        {
            if (this.CrystalConfiguration.SaveFormat == SaveFormat.Utf8)
            {
                rentMemory = TinyhandSerializer.SerializeObjectToUtf8RentMemory(this.data);
            }
            else
            {
                rentMemory = TinyhandSerializer.SerializeObjectToRentMemory(this.data);
            }
        }
        catch
        {
        }

        var hash = FarmHash.Hash64(rentMemory.Span);
        this.waypoint = default;
        this.Crystalizer.UpdateWaypoint(this, ref this.waypoint, hash);

        // Save immediately to fix the waypoint.
        _ = this.crystalFiler?.Save(rentMemory.ReadOnly, this.waypoint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetupData()
    {
        if (this.data is IStructualObject structualObject)
        {
            structualObject.SetupStructure(this);
        }
    }

    private void LogWaypoint(string prefix)
    {
        var logger = this.Crystalizer.UnitLogger.GetLogger<TData>();
        logger.TryGet(LogLevel.Error)?.Log($"{prefix}, {this.waypoint.ToString()}");
    }

    private void PrepareCrystalConfiguration()
    {
        var configuration = this.originalCrystalConfiguration;

        var saveFormat = configuration.SaveFormat;
        saveFormat = saveFormat == SaveFormat.Default ? this.Crystalizer.Options.DefaultSaveFormat : saveFormat;

        var savePolicy = configuration.SavePolicy;
        savePolicy = savePolicy == SavePolicy.Default ? this.Crystalizer.Options.DefaultSavePolicy : savePolicy;

        var saveInterval = configuration.SaveInterval;
        saveInterval = saveInterval == TimeSpan.Zero ? this.Crystalizer.Options.DefaultSaveInterval : saveInterval;

        var fileConfiguration = configuration.FileConfiguration;
        var filePath = fileConfiguration.Path;
        if (string.IsNullOrEmpty(filePath))
        {
            fileConfiguration = fileConfiguration with { Path = $"{typeof(TData).Name}{saveFormat.ToExtension()}", };
        }
        else if (Path.GetFileName(filePath).IndexOf('.') < 0)
        {
            fileConfiguration = fileConfiguration with { Path = $"{filePath}{saveFormat.ToExtension()}", };
        }

        if (this.Crystalizer.Options.DefaultBackup is { } globalBackup)
        {
            if (configuration.BackupFileConfiguration == null)
            {
                configuration = configuration with { BackupFileConfiguration = globalBackup.CombineFile(fileConfiguration.Path) };
            }

            if (configuration.StorageConfiguration is not null &&
                configuration.StorageConfiguration.BackupDirectoryConfiguration == null)
            {
                var storageConfiguration = configuration.StorageConfiguration with { BackupDirectoryConfiguration = globalBackup.CombineDirectory(configuration.StorageConfiguration.DirectoryConfiguration), };
                configuration = configuration with { StorageConfiguration = storageConfiguration, };
            }
        }

        if (saveFormat != configuration.SaveFormat ||
            savePolicy != configuration.SavePolicy ||
            saveInterval != configuration.SaveInterval ||
            fileConfiguration != configuration.FileConfiguration)
        {
            configuration = configuration with { SaveFormat = saveFormat, SavePolicy = savePolicy, SaveInterval = saveInterval, FileConfiguration = fileConfiguration, };
        }

        this.crystalConfiguration = configuration;
    }
}
