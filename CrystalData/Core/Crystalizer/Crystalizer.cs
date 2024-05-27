// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using CrystalData.Check;
using CrystalData.Filer;
using CrystalData.Journal;
using CrystalData.Storage;
using CrystalData.Unload;
using CrystalData.UserInterface;
using Tinyhand.IO;

#pragma warning disable SA1204

namespace CrystalData;

public class Crystalizer
{
    public const string CheckFile = "Crystal.check";
    public const int TaskIntervalInMilliseconds = 1_000;
    public const int PeriodicSaveInMilliseconds = 10_000;

    private class CrystalizerTask : TaskCore
    {
        public CrystalizerTask(Crystalizer crystalizer)
            : base(null, Process)
        {
            this.crystalizer = crystalizer;
        }

        private static async Task Process(object? parameter)
        {
            var core = (CrystalizerTask)parameter!;
            int elapsedMilliseconds = 0;
            while (await core.Delay(TaskIntervalInMilliseconds).ConfigureAwait(false))
            {
                await core.crystalizer.QueuedSave().ConfigureAwait(false);

                elapsedMilliseconds += TaskIntervalInMilliseconds;
                if (elapsedMilliseconds >= PeriodicSaveInMilliseconds)
                {
                    elapsedMilliseconds = 0;
                    await core.crystalizer.PeriodicSave().ConfigureAwait(false);
                }
            }
        }

        private Crystalizer crystalizer;
    }

    public Crystalizer(CrystalizerConfiguration configuration, CrystalizerOptions options, ICrystalDataQuery query, ILogger<Crystalizer> logger, UnitLogger unitLogger, IStorageKey storageKey)
    {
        this.configuration = configuration;
        this.GlobalDirectory = options.GlobalDirectory;
        this.DefaultBackup = options.DefaultBackup;
        this.GlobalStorage = options.GlobalStorage;
        this.EnableFilerLogger = options.EnableFilerLogger;
        this.RootDirectory = options.RootPath;
        this.FilerTimeout = options.FilerTimeout;
        this.MemoryUsageLimit = options.MemoryUsageLimit;
        this.ConcurrentUnload = options.ConcurrentUnload;
        this.UnloadTimeout = options.UnloadTimeout;
        if (string.IsNullOrEmpty(this.RootDirectory))
        {
            this.RootDirectory = Directory.GetCurrentDirectory();
        }

        this.DefaultSaveFormat = options.DefaultSaveFormat == SaveFormat.Default ? SaveFormat.Binary : options.DefaultSaveFormat;
        this.DefaultSavePolicy = options.DefaultSavePolicy == SavePolicy.Default ? SavePolicy.Manual : options.DefaultSavePolicy;
        this.DefaultSaveInterval = options.DefaultSaveInterval == TimeSpan.Zero ? CrystalConfiguration.DefaultSaveInterval : options.DefaultSaveInterval;

        this.Logger = logger;
        this.task = new(this);
        this.Query = query;
        this.QueryContinue = new CrystalDataQueryNo();
        this.UnitLogger = unitLogger;
        this.CrystalCheck = new(this.UnitLogger.GetLogger<CrystalCheck>());
        this.CrystalCheck.Load(Path.Combine(this.RootDirectory, CheckFile));
        this.Memory = new(this, this.CrystalCheck.MemoryStats);
        this.StorageKey = storageKey;

        foreach (var x in this.configuration.CrystalConfigurations)
        {
            // new CrystalImpl<TData>
            var crystal = (ICrystalInternal)Activator.CreateInstance(typeof(CrystalObject<>).MakeGenericType(x.Key), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { this, }, null)!;
            crystal.Configure(x.Value);

            this.typeToCrystal.TryAdd(x.Key, crystal);
            this.crystals.TryAdd(crystal, 0);
        }
    }

    #region FieldAndProperty

    public DirectoryConfiguration GlobalDirectory { get; }

    public DirectoryConfiguration? DefaultBackup { get; }

    public StorageConfiguration GlobalStorage { get; }

    public bool EnableFilerLogger { get; }

    public string RootDirectory { get; }

    public TimeSpan FilerTimeout { get; }

    public long MemoryUsageLimit { get; }

    public int ConcurrentUnload { get; }

    public TimeSpan UnloadTimeout { get; }

    public SaveFormat DefaultSaveFormat { get; set; }

    public SavePolicy DefaultSavePolicy { get; set; }

    public TimeSpan DefaultSaveInterval { get; set; }

    public IJournal? Journal { get; private set; }

    public JournalConfiguration? JournalConfiguration { get; private set; }

    public IStorageKey StorageKey { get; }

    public MemoryControl Memory { get; }

    internal ICrystalDataQuery Query { get; }

    internal ICrystalDataQuery QueryContinue { get; }

    internal UnitLogger UnitLogger { get; }

    internal ILogger Logger { get; }

    internal CrystalCheck CrystalCheck { get; }

    private CrystalizerConfiguration configuration;

    private CrystalizerTask task;
    private ThreadsafeTypeKeyHashtable<ICrystalInternal> typeToCrystal = new(); // Type to ICrystal
    private ConcurrentDictionary<ICrystalInternal, int> crystals = new(); // All crystals
    private ConcurrentDictionary<uint, ICrystalInternal> planeToCrystal = new(); // Plane to crystal
    private ConcurrentDictionary<ICrystal, int> saveQueue = new(); // Save queue

    private object syncObject = new();
    private IRawFiler? localFiler;
    private Dictionary<string, IRawFiler> bucketToS3Filer = new();
    private Dictionary<StorageConfiguration, IStorage> configurationToStorage = new();

    #endregion

    #region Resolvers

    /*public (IFiler Filer, PathConfiguration FixedConfiguration) ResolveFiler(PathConfiguration configuration)
    {
        var resolved = this.ResolveRawFiler(configuration);
        return (new RawFilerToFiler(this, resolved.RawFiler, resolved.FixedConfiguration.Path), resolved.FixedConfiguration);
    }*/

    public (IFiler Filer, FileConfiguration FixedConfiguration) ResolveFiler(FileConfiguration configuration)
    {
        var resolved = this.ResolveRawFiler(configuration);
        return (new RawFilerToFiler(this, resolved.RawFiler, resolved.FixedConfiguration.Path), resolved.FixedConfiguration);
    }

    public (IFiler Filer, DirectoryConfiguration FixedConfiguration) ResolveFiler(DirectoryConfiguration configuration)
    {
        var resolved = this.ResolveRawFiler(configuration);
        return (new RawFilerToFiler(this, resolved.RawFiler, resolved.FixedConfiguration.Path), resolved.FixedConfiguration);
    }

    /*public (IRawFiler RawFiler, PathConfiguration FixedConfiguration) ResolveRawFiler(PathConfiguration configuration)
    {
        lock (this.syncFiler)
        {
            if (configuration is GlobalFileConfiguration)
            {// Global file
                configuration = this.GlobalMain.CombineFile(configuration.Path);
            }
            else if (configuration is GlobalDirectoryConfiguration directoryConfiguration)
            {// Global directory
                configuration = this.GlobalMain.CombineDirectory(directoryConfiguration);
            }

            if (configuration is EmptyFileConfiguration ||
                configuration is EmptyDirectoryConfiguration)
            {// Empty file or directory
                return (EmptyFiler.Default, configuration);
            }
            else if (configuration is LocalFileConfiguration ||
                configuration is LocalDirectoryConfiguration)
            {// Local file or directory
                if (this.localFiler == null)
                {
                    this.localFiler ??= new LocalFiler();
                }

                return (this.localFiler, configuration);
            }
            else if (configuration is S3FileConfiguration s3FilerConfiguration)
            {// S3 file
                return (ResolveS3Filer(s3FilerConfiguration.Bucket), configuration);
            }
            else if (configuration is S3DirectoryConfiguration s3DirectoryConfiguration)
            {// S3 directory
                return (ResolveS3Filer(s3DirectoryConfiguration.Bucket), configuration);
            }
            else
            {
                ThrowConfigurationNotRegistered(configuration.GetType());
                return default!;
            }
        }

        IRawFiler ResolveS3Filer(string bucket)
        {
            if (!this.bucketToS3Filer.TryGetValue(bucket, out var filer))
            {
                filer = new S3Filer(bucket);
                this.bucketToS3Filer.TryAdd(bucket, filer);
            }

            return filer;
        }
    }*/

    public (IRawFiler RawFiler, FileConfiguration FixedConfiguration) ResolveRawFiler(FileConfiguration configuration)
    {
        lock (this.syncObject)
        {
            if (configuration is GlobalFileConfiguration)
            {// Global file
                configuration = this.GlobalDirectory.CombineFile(configuration.Path);
            }

            if (configuration is EmptyFileConfiguration)
            {// Empty file
                return (EmptyFiler.Default, configuration);
            }
            else if (configuration is LocalFileConfiguration)
            {// Local file
                if (this.localFiler == null)
                {
                    this.localFiler ??= new LocalFiler();
                }

                return (this.localFiler, configuration);
            }
            else if (configuration is S3FileConfiguration s3Configuration)
            {// S3 file
                if (!this.bucketToS3Filer.TryGetValue(s3Configuration.Bucket, out var filer))
                {
                    filer = new S3Filer(s3Configuration.Bucket);
                    this.bucketToS3Filer.TryAdd(s3Configuration.Bucket, filer);
                }

                return (filer, configuration);
            }
            else
            {
                ThrowConfigurationNotRegistered(configuration.GetType());
                return default!;
            }
        }
    }

    public (IRawFiler RawFiler, DirectoryConfiguration FixedConfiguration) ResolveRawFiler(DirectoryConfiguration configuration)
    {
        lock (this.syncObject)
        {
            if (configuration is GlobalDirectoryConfiguration)
            {// Global directory
                configuration = this.GlobalDirectory.CombineDirectory(configuration);
            }

            if (configuration is EmptyDirectoryConfiguration)
            {// Empty directory
                return (EmptyFiler.Default, configuration);
            }
            else if (configuration is LocalDirectoryConfiguration)
            {// Local directory
                if (this.localFiler == null)
                {
                    this.localFiler ??= new LocalFiler();
                }

                return (this.localFiler, configuration);
            }
            else if (configuration is S3DirectoryConfiguration s3Configuration)
            {// S3 directory
                if (!this.bucketToS3Filer.TryGetValue(s3Configuration.Bucket, out var filer))
                {
                    filer = new S3Filer(s3Configuration.Bucket);
                    this.bucketToS3Filer.TryAdd(s3Configuration.Bucket, filer);
                }

                return (filer, configuration);
            }
            else
            {
                ThrowConfigurationNotRegistered(configuration.GetType());
                return default!;
            }
        }
    }

    public IStorage ResolveStorage(ref StorageConfiguration configuration)
    {
        lock (this.syncObject)
        {
            IStorage? storage;
            if (configuration is GlobalStorageConfiguration globalStorageConfiguration)
            {// Default storage
                if (this.GlobalStorage is GlobalStorageConfiguration)
                {// Recursive
                    configuration = EmptyStorageConfiguration.Default;
                }
                else
                {
                    configuration = this.GlobalStorage;
                }
            }

            if (configuration is EmptyStorageConfiguration emptyStorageConfiguration)
            {// Empty storage
                storage = EmptyStorage.Default;
            }
            else if (configuration is SimpleStorageConfiguration simpleStorageConfiguration)
            {
                if (simpleStorageConfiguration.DirectoryConfiguration is GlobalDirectoryConfiguration globalDirectoryConfiguration)
                {
                    configuration = configuration with { DirectoryConfiguration = this.GlobalDirectory.CombineDirectory(globalDirectoryConfiguration), };
                }

                if (simpleStorageConfiguration.BackupDirectoryConfiguration is GlobalDirectoryConfiguration backupDirectoryConfiguration)
                {
                    configuration = configuration with { BackupDirectoryConfiguration = this.GlobalDirectory.CombineDirectory(backupDirectoryConfiguration), };
                }

                if (!this.configurationToStorage.TryGetValue(configuration, out storage))
                {
                    storage = new SimpleStorage(this);
                    this.configurationToStorage.TryAdd(configuration, storage);
                }
            }
            else
            {
                ThrowConfigurationNotRegistered(configuration.GetType());
                return default!;
            }

            storage.SetTimeout(this.FilerTimeout);
            return storage;
        }
    }

    #endregion

    #region Main

    public void ResetConfigurations()
    {
        foreach (var x in this.configuration.CrystalConfigurations)
        {
            if (this.typeToCrystal.TryGetValue(x.Key, out var crystal))
            {
                crystal.Configure(x.Value);
            }
        }
    }

    public async Task<CrystalResult> SaveConfigurations(FileConfiguration configuration)
    {
        var data = new Dictionary<string, CrystalConfiguration>();
        foreach (var x in this.configuration.CrystalConfigurations)
        {
            if (this.typeToCrystal.TryGetValue(x.Key, out var crystal) &&
                x.Key.FullName is { } name)
            {
                data[name] = crystal.OriginalCrystalConfiguration;
            }
        }

        var resolved = this.ResolveFiler(configuration);
        var result = await resolved.Filer.PrepareAndCheck(PrepareParam.NoQuery<Crystalizer>(this), resolved.FixedConfiguration).ConfigureAwait(false);
        if (result.IsFailure())
        {
            return result;
        }

        var bytes = TinyhandSerializer.SerializeToUtf8(data);
        result = await resolved.Filer.WriteAsync(0, new(bytes)).ConfigureAwait(false);

        return result;
    }

    public async Task<CrystalResult> LoadConfigurations(FileConfiguration configuration)
    {
        var resolved = this.ResolveFiler(configuration);
        var result = await resolved.Filer.PrepareAndCheck(PrepareParam.NoQuery<Crystalizer>(this), resolved.FixedConfiguration).ConfigureAwait(false);
        if (result.IsFailure())
        {
            return result;
        }

        var readResult = await resolved.Filer.ReadAsync(0, -1).ConfigureAwait(false);
        if (readResult.IsFailure)
        {
            return readResult.Result;
        }

        try
        {
            var data = TinyhandSerializer.DeserializeFromUtf8<Dictionary<string, CrystalConfiguration>>(readResult.Data.Memory);
            if (data == null)
            {
                return CrystalResult.DeserializeError;
            }

            var nameToCrystal = new Dictionary<string, ICrystal>();
            foreach (var x in this.typeToCrystal.ToArray())
            {
                if (x.Key.FullName is { } name)
                {
                    nameToCrystal[name] = x.Value;
                }
            }

            foreach (var x in data)
            {
                if (nameToCrystal.TryGetValue(x.Key, out var crystal))
                {
                    crystal.Configure(x.Value);
                }
            }

            return CrystalResult.Success;
        }
        catch
        {
            return CrystalResult.DeserializeError;
        }
        finally
        {
            readResult.Return();
        }
    }

    public async Task<CrystalResult> PrepareAndLoadAll(bool useQuery = true)
    {
        // Check file
        if (!this.CrystalCheck.SuccessfullyLoaded)
        {
            if (await this.Query.NoCheckFile().ConfigureAwait(false) == AbortOrContinue.Abort)
            {
                return CrystalResult.NotFound;
            }
            else
            {
                this.CrystalCheck.SuccessfullyLoaded = true;
            }
        }

        // Journal
        var result = await this.PrepareJournal(useQuery).ConfigureAwait(false);
        if (result.IsFailure())
        {
            return result;
        }

        // Crystals
        var crystals = this.crystals.Keys.ToArray();
        // var list = new List<string>();
        foreach (var x in crystals)
        {
            result = await x.PrepareAndLoad(useQuery).ConfigureAwait(false);
            if (result.IsFailure())
            {
                return result;
            }

            // list.Add(x.Data.GetType().Name);
        }

        // Dump Plane
        // this.DumpPlane();

        // Read journal
        await this.ReadJournal().ConfigureAwait(false);

        // Save crystal check
        this.CrystalCheck.Save();
        this.CrystalCheck.ClearShortcutPosition();

        // this.logger.TryGet()?.Log($"Prepared - {string.Join(", ", list)}");

        return CrystalResult.Success;
    }

    public async Task SaveAll()
    {
        var crystals = this.crystals.Keys.ToArray();
        foreach (var x in crystals)
        {
            await x.Save(UnloadMode.NoUnload).ConfigureAwait(false);
        }

        this.CrystalCheck.Save();
    }

    public async Task SaveAndUnloadAll()
    {
        var goshujin = new UnloadTask.GoshujinClass();
        foreach (var x in this.crystals.Keys)
        {
            goshujin.Add(new(x));
        }

        var unloadTasks = new Task[this.ConcurrentUnload];
        for (var i = 0; i < this.ConcurrentUnload; i++)
        {
            unloadTasks[i] = UnloadTaskExtension.UnloadTask(this, goshujin);
        }

        await Task.WhenAll(unloadTasks).ConfigureAwait(false);

        this.CrystalCheck.Save();
    }

    public async Task SaveAllAndTerminate()
    {
        await this.SaveAndUnloadAll();

        // Save/Terminate journal
        if (this.Journal is { } journal)
        {
            await journal.SaveJournalAsync().ConfigureAwait(false);
            await journal.TerminateAsync().ConfigureAwait(false);
        }

        // Terminate filers/journal
        var tasks = new List<Task>();
        lock (this.syncObject)
        {
            if (this.localFiler is not null)
            {
                tasks.Add(this.localFiler.TerminateAsync());
                this.localFiler = null;
            }

            foreach (var x in this.bucketToS3Filer.Values)
            {
                tasks.Add(x.TerminateAsync());
            }

            this.bucketToS3Filer.Clear();
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        var stat = this.Memory.GetStat();
        this.Logger.TryGet()?.Log($"Terminated - {stat.MemoryUsage} ({stat.MemoryCount})"); // {this.MemoryUsageLimit}
    }

    public void AddToSaveQueue(ICrystal crystal)
    {
        this.saveQueue.TryAdd(crystal, 0);
    }

    public async Task<CrystalResult[]> DeleteAll()
    {
        var tasks = this.crystals.Keys.Select(x => x.Delete()).ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }

    public void DeleteDirectory(DirectoryConfiguration directoryConfiguration)
    {
        if (directoryConfiguration is GlobalDirectoryConfiguration globalDirectoryConfiguration)
        {
            directoryConfiguration = this.GlobalDirectory.CombineDirectory(globalDirectoryConfiguration);
        }

        if (directoryConfiguration is LocalDirectoryConfiguration localDirectoryConfiguration)
        {
            try
            {
                Directory.Delete(localDirectoryConfiguration.Path, true);
            }
            catch
            {
            }
        }
    }

    public ICrystal<TData> CreateCrystal<TData>(CrystalConfiguration? configuration = null, bool managedByCrystalizer = true)
        where TData : class, ITinyhandSerialize<TData>, ITinyhandReconstruct<TData>
    {
        var crystal = new CrystalObject<TData>(this);
        if (managedByCrystalizer)
        {
            this.crystals.TryAdd(crystal, 0);
        }

        if (configuration is not null)
        {
            ((ICrystal)crystal).Configure(configuration);
        }

        return crystal;
    }

    public ICrystal<TData> GetOrCreateCrystal<TData>(CrystalConfiguration configuration)
        where TData : class, ITinyhandSerialize<TData>, ITinyhandReconstruct<TData>
    {
        if (this.typeToCrystal.TryGetValue(typeof(TData), out var crystal) &&
            crystal is ICrystal<TData> crystalData)
        {
            return crystalData;
        }

        var crystalObject = new CrystalObject<TData>(this);
        this.crystals.TryAdd(crystalObject, 0);
        ((ICrystal)crystalObject).Configure(configuration);
        return crystalObject;
    }

    public ICrystal<TData> GetCrystal<TData>()
        where TData : class, ITinyhandSerialize<TData>, ITinyhandReconstruct<TData>
    {
        if (!this.typeToCrystal.TryGetValue(typeof(TData), out var c) ||
            c is not ICrystal<TData> crystal)
        {
            ThrowTypeNotRegistered(typeof(TData));
            return default!;
        }

        return crystal;
    }

    public async Task MergeJournalForTest()
    {
        if (this.Journal is SimpleJournal simpleJournal)
        {
            await simpleJournal.Merge(true).ConfigureAwait(false);
        }
    }

    public async Task<bool> TestJournalAll()
    {
        var crystals = this.crystals.Keys.ToArray();
        var result = true;
        foreach (var x in crystals)
        {
            if (await x.TestJournal().ConfigureAwait(false) == false)
            {
                result = false;
            }
        }

        return result;
    }

    public async Task SaveJournal()
    {
        // Save journal
        if (this.Journal is { } journal)
        {
            await journal.SaveJournalAsync().ConfigureAwait(false);
        }
    }

    #endregion

    #region Waypoint/Plane

    internal void UpdateWaypoint(ICrystalInternal crystal, ref Waypoint waypoint, ulong hash)
    {
        var plane = waypoint.Plane;
        if (plane == 0)
        {
            while (true)
            {
                plane = RandomVault.Pseudo.NextUInt32();
                if (plane != 0 && this.planeToCrystal.TryAdd(plane, crystal))
                {// Success
                    break;
                }
            }
        }

        // Add journal
        ulong journalPosition;
        if (this.Journal != null)
        {
            this.Journal.GetWriter(JournalType.Waypoint, out var writer);
            writer.Write(plane);
            writer.Write(hash);
            journalPosition = this.Journal.Add(ref writer);
        }
        else
        {
            journalPosition = waypoint.JournalPosition + 1;
        }

        waypoint = new(journalPosition, plane, hash);
    }

    /*internal void UpdatePlane(ICrystalInternal crystal, ref Waypoint waypoint, ulong hash, ulong startingPosition)
    {
        if (waypoint.CurrentPlane != 0)
        {// Remove the current plane
            this.planeToCrystal.TryRemove(waypoint.CurrentPlane, out _);
        }

        // Next plane
        var nextPlane = waypoint.NextPlane;
        if (nextPlane == 0)
        {
            while (true)
            {
                nextPlane = RandomVault.Pseudo.NextUInt32();
                if (nextPlane != 0 && this.planeToCrystal.TryAdd(nextPlane, crystal))
                {// Success
                    break;
                }
            }
        }

        // New plane
        uint newPlane;
        while (true)
        {
            newPlane = RandomVault.Pseudo.NextUInt32();
            if (newPlane != 0 && this.planeToCrystal.TryAdd(newPlane, crystal))
            {// Success
                break;
            }
        }

        // Current/Next -> Next/New

        // Add journal
        ulong journalPosition;
        ulong depth = 0;
        if (this.Journal != null)
        {
            this.Journal.GetWriter(JournalType.Waypoint, out var writer);
            writer.Write(newPlane);
            writer.Write(hash);
            journalPosition = this.Journal.Add(writer);

            depth = journalPosition - startingPosition;
            if (depth < 0 || depth > Waypoint.MaxDepth || startingPosition == 0)
            {
                depth = 0;
            }
        }
        else
        {
            journalPosition = waypoint.JournalPosition + 1;
        }

        waypoint = new(journalPosition, nextPlane, newPlane, hash, (uint)depth);
    }*/

    internal void RemovePlane(Waypoint waypoint)
    {
        if (waypoint.Plane != 0)
        {
            this.planeToCrystal.TryRemove(waypoint.Plane, out _);
            // this.CrystalCheck.TryRemovePlane(waypoint.CurrentPlane);
        }
    }

    internal void SetPlane(ICrystalInternal crystal, ref Waypoint waypoint)
    {
        if (waypoint.Plane != 0)
        {
            this.planeToCrystal[waypoint.Plane] = crystal;
        }
    }

    /*[MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ulong AddStartingPoint(uint plane)
    {
        if (this.Journal is { } journal)
        {
            journal.GetWriter(JournalType.Startingpoint, out var writer);
            return journal.Add(writer);
        }
        else
        {
            return 0;
        }
    }*/

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ulong GetJournalPosition()
    {
        if (this.Journal is { } journal)
        {
            return journal.GetCurrentPosition();
        }
        else
        {
            return 0;
        }
    }

    #endregion

    #region Misc

    internal static string GetRootedFile(Crystalizer? crystalizer, string file)
        => crystalizer == null ? file : PathHelper.GetRootedFile(crystalizer.RootDirectory, file);

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowTypeNotRegistered(Type type)
    {
        throw new InvalidOperationException($"The specified data type '{type.Name}' is not registered. Register the data type within ConfigureCrystal().");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowConfigurationNotRegistered(Type type)
    {
        throw new InvalidOperationException($"The specified configuration type '{type.Name}' is not registered.");
    }

    internal bool DeleteInternal(ICrystalInternal crystal)
    {
        if (!this.typeToCrystal.TryGetValue(crystal.DataType, out _))
        {// Created crystals
            return this.crystals.TryRemove(crystal, out _);
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ICrystal GetCrystal(Type type)
    {
        if (!this.typeToCrystal.TryGetValue(type, out var crystal))
        {
            ThrowTypeNotRegistered(type);
        }

        return crystal!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal object GetObject(Type type)
    {
        if (!this.typeToCrystal.TryGetValue(type, out var crystal))
        {
            ThrowTypeNotRegistered(type);
        }

        return crystal!.Data;
    }

    private Task PeriodicSave()
    {
        var tasks = new List<Task>();
        var crystals = this.crystals.Keys.ToArray();
        var utc = DateTime.UtcNow;
        foreach (var x in crystals)
        {
            if (x.TryPeriodicSave(utc) is { } task)
            {
                tasks.Add(task);
            }
        }

        return Task.WhenAll(tasks);
    }

    private Task QueuedSave()
    {
        var tasks = new List<Task>();
        var array = this.saveQueue.Keys.ToArray();
        this.saveQueue.Clear();
        foreach (var x in array)
        {
            if (x.State == CrystalState.Prepared)
            {
                tasks.Add(x.Save());
            }
        }

        return Task.WhenAll(tasks);
    }

    #endregion

    #region Journal

    private async Task<CrystalResult> PrepareJournal(bool useQuery = true)
    {
        if (this.Journal == null)
        {// New journal
            var configuration = this.configuration.JournalConfiguration;
            if (configuration is EmptyJournalConfiguration)
            {
                return CrystalResult.Success;
            }
            else if (configuration is SimpleJournalConfiguration simpleJournalConfiguration)
            {
                if (this.DefaultBackup is { } globalBackup)
                {
                    if (simpleJournalConfiguration.BackupDirectoryConfiguration == null)
                    {
                        simpleJournalConfiguration = simpleJournalConfiguration with
                        {
                            BackupDirectoryConfiguration = globalBackup.CombineDirectory(simpleJournalConfiguration.DirectoryConfiguration),
                        };
                    }
                }

                var simpleJournal = new SimpleJournal(this, simpleJournalConfiguration, this.UnitLogger.GetLogger<SimpleJournal>());
                this.Journal = simpleJournal;
                this.JournalConfiguration = simpleJournalConfiguration;
            }
            else
            {
                return CrystalResult.NotFound;
            }
        }

        return await this.Journal.Prepare(PrepareParam.New<Crystalizer>(this, useQuery)).ConfigureAwait(false);
    }

    private async Task ReadJournal()
    {
        if (this.Journal is { } journal)
        {// Load journal
            ulong position = journal.GetCurrentPosition();

            var array = this.crystals.Keys.ToArray();
            for (var i = 0; i < array.Length; i++)
            {
                var waypoint = array[i].Waypoint;
                this.CrystalCheck.TryGetPlanePosition(waypoint, out var shortcutPosition);
                /*f (!this.CrystalCheck.TryGetPlanePosition(waypoint, out var shortcutPosition))
                {
                    this.logger.TryGet(LogLevel.Error)?.Log($"No shortcut position: {array[i].Value.DataType.Name}");
                }*/

                var max = shortcutPosition.CircularCompareTo(waypoint.JournalPosition) > 0 ? shortcutPosition : waypoint.JournalPosition;
                max = Math.Max(max, 1);
                if (position.CircularCompareTo(max) > 0)
                {
                    position = max;
                }
            }

            HashSet<uint> restored = new();
            var failure = false;
            var startPosition = position;
            ulong endPosition = 0;
            while (position != 0)
            {
                endPosition = position;
                var journalResult = await journal.ReadJournalAsync(position).ConfigureAwait(false);
                if (journalResult.NextPosition == 0)
                {
                    break;
                }

                try
                {
                    this.ProcessJournal(position, journalResult.Data.Memory, ref restored, ref failure);
                }
                finally
                {
                    journalResult.Data.Return();
                }

                position = journalResult.NextPosition;
            }

            this.Logger.TryGet(LogLevel.Debug)?.Log($"Journal read {startPosition} - {endPosition}");
            if (failure)
            {
                this.Logger.TryGet(LogLevel.Error)?.Log(CrystalDataHashed.Journal.ReadFailure);
            }

            if (restored.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var x in restored)
                {
                    if (this.planeToCrystal.TryGetValue(x, out var crystal))
                    {
                        sb.Append($"{crystal.DataType.FullName}, ");
                    }
                }

                this.Logger.TryGet(LogLevel.Error)?.Log(CrystalDataHashed.Journal.Restored, sb.ToString());
            }
        }
    }

    private void ProcessJournal(ulong position, Memory<byte> data, ref HashSet<uint> restored, ref bool failure)
    {
        var reader = new TinyhandReader(data.Span);
        while (reader.Consumed < data.Length)
        {
            if (!reader.TryReadRecord(out var length, out var journalType))
            {
                this.Logger.TryGet(LogLevel.Error)?.Log(CrystalDataHashed.Journal.Corrupted);
                return;
            }

            var fork = reader.Fork();
            try
            {
                if (journalType == JournalType.Startingpoint)
                {
                }
                else if (journalType == JournalType.Waypoint)
                {
                    reader.ReadUInt32();
                    reader.ReadUInt64();
                }
                else if (journalType == JournalType.Record)
                {
                    reader.Read_Locator();
                    var plane = reader.ReadUInt32();
                    if (this.planeToCrystal.TryGetValue(plane, out var crystal))
                    {
                        if (crystal.Data is IStructualObject journalObject)
                        {
                            var currentPosition = position + (ulong)reader.Consumed;
                            if (currentPosition.CircularCompareTo(crystal.Waypoint.JournalPosition) > 0)
                            {
                                if (journalObject.ReadRecord(ref reader))
                                {// Success
                                    // this.logger.TryGet(LogLevel.Debug)?.Log($"Journal read, Plane: {plane}, Length: {length} => {crystal.GetType().FullName}");
                                    restored.Add(plane);
                                }
                                else
                                {// Failure
                                    failure = true;
                                }
                            }
                        }
                    }
                }
                else
                {
                }
            }
            catch
            {
            }
            finally
            {
                reader = fork;
                if (!reader.TryAdvance(length))
                {
                    failure = true;
                    reader.TryAdvance(reader.Remaining);
                }
            }
        }
    }

    #endregion

    private void DumpPlane()
    {
        foreach (var x in this.planeToCrystal)
        {
            this.Logger.TryGet(LogLevel.Debug)?.Log($"Plane: {x.Key} = {x.Value.GetType().FullName}");
        }
    }
}
