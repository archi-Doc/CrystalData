// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Filer;

public class CrystalFiler
{
    internal class Output
    {
        public Output(CrystalFiler crystalFiler, FileConfiguration fileConfiguration)
        {
            this.crystalFiler = crystalFiler;
            this.fileConfiguration = fileConfiguration;
        }

        #region PropertyAndField

        private CrystalFiler crystalFiler;
        private FileConfiguration fileConfiguration;

        private IRawFiler? rawFiler;
        private Lock lockObject = new();
        private string prefix = string.Empty; // "Directory/File."
        private string extension = string.Empty; // string.Empty or ".extension"
        private SortedSet<Waypoint>? waypoints;

        #endregion

        public Waypoint GetLatestWaypoint()
        {
            using (this.lockObject.EnterScope())
            {
                return this.waypoints == null ? Waypoint.Invalid : this.waypoints.LastOrDefault();
            }
        }

        public async Task<CrystalResult> PrepareAndCheck(PrepareParam param, CrystalConfiguration configuration)
        {
            if (this.rawFiler == null)
            {
                (this.rawFiler, this.fileConfiguration) = this.crystalFiler.Crystalizer.ResolveRawFiler(this.fileConfiguration);
                var result = await this.rawFiler.PrepareAndCheck(param, this.fileConfiguration).ConfigureAwait(false);
                if (result.IsFailure())
                {
                    return result;
                }
            }

            using (this.lockObject.EnterScope())
            {
                // identifier/extension
                this.extension = Path.GetExtension(this.fileConfiguration.Path) ?? string.Empty;
                this.prefix = this.fileConfiguration.Path.Substring(0, this.fileConfiguration.Path.Length - this.extension.Length) + ".";
            }

            return CrystalResult.Success;
        }

        public async Task ListData()
        {// Prefix/Data.Waypoint.Extension or Prefix/Data.Extension
            if (this.rawFiler == null)
            {
                return;
            }
            else if (this.waypoints != null)
            {// Already loaded
                return;
            }

            var listResult = await this.rawFiler.ListAsync(this.prefix).ConfigureAwait(false); // "Folder/Data."

            using (this.lockObject.EnterScope())
            {
                this.waypoints ??= new();

                foreach (var x in listResult.Where(a => a.IsFile))
                {
                    var path = x.Path; // {this.prefix}.waypoint{this.extension}
                    if (!string.IsNullOrEmpty(this.extension))
                    {
                        if (path.EndsWith(this.extension))
                        {// Prefix/Data.Waypoint.Extension or Prefix/Data.Extension
                            path = path.Substring(0, path.Length - this.extension.Length);
                        }
                        else
                        {// No .Extension
                            continue;
                        }
                    }

                    if (path.Length < (Waypoint.LengthInBase32 + this.prefix.Length))
                    {
                        continue;
                    }

                    var waypointString = path.Substring(path.Length - Waypoint.LengthInBase32, Waypoint.LengthInBase32);
                    path = path.Substring(0, path.Length - Waypoint.LengthInBase32);
                    if (!StorageHelper.EndsWith_SlashInsensitive(path, this.prefix))
                    {
                        continue;
                    }

                    if (Waypoint.TryParse(waypointString, out var waypoint))
                    {// Data.Waypoint.Extension
                        this.waypoints.Add(waypoint);
                    }
                }
            }
        }

        public Task<CrystalResult> Save(BytePool.RentReadOnlyMemory rentMemory, Waypoint waypoint)
        {
            if (this.rawFiler == null)
            {
                return Task.FromResult(CrystalResult.NotPrepared);
            }

            if (!this.crystalFiler.IsProtected)
            {// Prefix.Extension
                return this.rawFiler.WriteAsync(this.GetFilePath(), 0, rentMemory);
            }

            using (this.lockObject.EnterScope())
            {
                this.waypoints ??= new();
                this.waypoints.Add(waypoint);
            }

            var path = this.GetFilePath(waypoint);
            return this.rawFiler.WriteAsync(path, 0, rentMemory);
        }

        public Task<CrystalResult> LimitNumberOfFiles()
        {
            if (this.rawFiler == null)
            {
                return Task.FromResult(CrystalResult.NotPrepared);
            }
            else if (this.waypoints == null)
            {
                return Task.FromResult(CrystalResult.Success);
            }

            var numberOfFiles = this.crystalFiler.configuration.NumberOfFileHistories;
            if (numberOfFiles < 1)
            {
                numberOfFiles = 1;
            }

            Waypoint[] array;
            string[] pathArray;
            using (this.lockObject.EnterScope())
            {
                array = this.waypoints.Take(this.waypoints.Count - numberOfFiles).ToArray();
                if (array.Length == 0)
                {
                    return Task.FromResult(CrystalResult.Success);
                }

                pathArray = array.Select(x => this.GetFilePath(x)).ToArray();

                foreach (var x in array)
                {
                    this.waypoints.Remove(x);
                }
            }

            var tasks = pathArray.Select(x => this.rawFiler.DeleteAsync(x)).ToArray();
            return Task.WhenAll(tasks).ContinueWith(x => CrystalResult.Success);
        }

        public async Task<(CrystalObjectResult<TData> Result, Waypoint Waypoint, string Path)> LoadLatest<TData>(PrepareParam param, SaveFormat formatHint, TData? singletonData)
            where TData : class, ITinyhandSerializable<TData>, ITinyhandReconstructable<TData>
        {
            if (this.rawFiler == null)
            {
                return (new(CrystalResult.NotPrepared), Waypoint.Invalid, string.Empty);
            }

            string path;
            if (!this.crystalFiler.IsProtected)
            {// No file history
                path = this.GetFilePath();
                var result = await this.rawFiler.ReadAsync(path, 0, -1).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    var hash = FarmHash.Hash64(result.Data.Span);
                    var r = SerializeHelper.TryDeserialize<TData>(result.Data.Span, formatHint, true, singletonData);
                    result.Return();
                    if (r.Data is not null)
                    {// Success
                        return (new(CrystalResult.Success, r.Data), new(Waypoint.InvalidJournalPosition, 0, hash), path);
                    }

                    _ = this.rawFiler.DeleteAsync(path);
                    return (new(CrystalResult.DeserializationFailed), Waypoint.Invalid, string.Empty);
                }

                // List data
                await this.ListData().ConfigureAwait(false);
            }

            var array = this.GetReverseWaypointArray();
            foreach (var x in array)
            {
                path = this.GetFilePath(x);
                var result = await this.rawFiler.ReadAsync(path, 0, -1).ConfigureAwait(false);
                if (result.IsSuccess)
                {// Read successful
                    if (FarmHash.Hash64(result.Data.Memory.Span) == x.Hash)
                    {
                        var r = SerializeHelper.TryDeserialize<TData>(result.Data.Span, formatHint, true, singletonData);
                        result.Return();
                        if (r.Data is not null)
                        {
                            return (new(CrystalResult.Success, r.Data), x, path);
                        }
                    }

                    // Checksum mismatch or deserialization error.
                    _ = this.rawFiler.DeleteAsync(path);
                    this.TryDeleteWaypoint(x);
                }
            }

            return (new(CrystalResult.NotFound), Waypoint.Invalid, string.Empty);
        }

        public Task<CrystalResult> DeleteAllAsync()
        {
            if (this.rawFiler == null)
            {
                return Task.FromResult(CrystalResult.NotPrepared);
            }
            else if (this.waypoints == null)
            {
                return Task.FromResult(CrystalResult.Success);
            }

            List<string> pathList;
            using (this.lockObject.EnterScope())
            {
                pathList = this.waypoints.Select(x => this.GetFilePath(x)).ToList();
                pathList.Add(this.GetFilePath());
                this.waypoints.Clear();
            }

            var tasks = pathList.Select(x => this.rawFiler.DeleteAsync(x)).ToArray();
            return Task.WhenAll(tasks).ContinueWith(x => CrystalResult.Success);
        }

        internal Waypoint[] GetWaypoints()
        {
            using (this.lockObject.EnterScope())
            {
                return this.waypoints == null ? Array.Empty<Waypoint>() : this.waypoints.ToArray();
            }
        }

        internal async Task<CrystalMemoryOwnerResult> LoadWaypoint(Waypoint waypoint)
        {
            if (this.rawFiler is null)
            {
                return new(CrystalResult.NotPrepared);
            }

            var path = this.GetFilePath(waypoint);
            var result = await this.rawFiler.ReadAsync(path, 0, -1).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return result;
            }

            if (FarmHash.Hash64(result.Data.Memory.Span) != waypoint.Hash)
            {
                return new(CrystalResult.CorruptedData);
            }

            // Success
            return result;
        }

        private string GetFilePath()
        {
            return $"{this.prefix.Substring(0, this.prefix.Length - 1)}{this.extension}";
        }

        private string GetFilePath(Waypoint waypoint)
        {
            return $"{this.prefix}{waypoint.ToBase32()}{this.extension}";
        }

        private void TryDeleteWaypoint(Waypoint waypoint)
        {
            using (this.lockObject.EnterScope())
            {
                if (this.waypoints is { } waypoints)
                {
                    waypoints.Remove(waypoint);
                }
            }
        }

        private Waypoint[] GetReverseWaypointArray()
        {
            using (this.lockObject.EnterScope())
            {
                if (this.waypoints == null)
                {
                    return Array.Empty<Waypoint>();
                }

                return this.waypoints.Reverse().ToArray();
            }
        }

        private bool TryGetLatestWaypoint(out Waypoint waypoint)
        {
            using (this.lockObject.EnterScope())
            {
                if (this.waypoints == null || this.waypoints.Count == 0)
                {
                    waypoint = default;
                    return false;
                }

                waypoint = this.waypoints.Last();
                return true;
            }
        }
    }

    #region PropertyAndField

    public Crystalizer Crystalizer { get; }

    private readonly ILogger logger;
    private CrystalConfiguration configuration;
    private Output? main;
    private Output? backup;

    public bool IsProtected => this.configuration.HasFileHistories;

    internal Output? Main => this.main;

    #endregion

    public CrystalFiler(Crystalizer crystalizer)
    {
        this.Crystalizer = crystalizer;
        this.configuration = CrystalConfiguration.Default;
        this.logger = this.Crystalizer.UnitLogger.GetLogger<CrystalFiler>();
    }

    public async Task<CrystalResult> PrepareAndCheck(PrepareParam param, CrystalConfiguration configuration)
    {
        this.configuration = configuration;

        // Output
        this.main ??= new(this, this.configuration.FileConfiguration);
        var result = await this.main.PrepareAndCheck(param, this.configuration).ConfigureAwait(false);
        if (result.IsFailure())
        {
            return result;
        }

        if (this.configuration.BackupFileConfiguration != null)
        {
            this.backup ??= new(this, this.configuration.BackupFileConfiguration);
            result = await this.backup.PrepareAndCheck(param, this.configuration).ConfigureAwait(false);
            if (result.IsFailure())
            {
                return result;
            }
        }

        if (this.IsProtected)
        {// List data
            await this.main.ListData().ConfigureAwait(false);
            if (this.backup is not null)
            {
                await this.backup.ListData().ConfigureAwait(false);
            }
        }

        return CrystalResult.Success;
    }

    public async Task<CrystalResult> Save(BytePool.RentReadOnlyMemory rentMemory, Waypoint waypoint)
    {
        if (this.main is null)
        {
            return CrystalResult.NotPrepared;
        }

        var result = await this.main.Save(rentMemory, waypoint).ConfigureAwait(false);
        _ = this.backup?.Save(rentMemory, waypoint);
        return result;
    }

    public async Task<CrystalResult> LimitNumberOfFiles()
    {
        if (this.main is null)
        {
            return CrystalResult.NotPrepared;
        }

        var result = await this.main.LimitNumberOfFiles().ConfigureAwait(false);
        _ = this.backup?.LimitNumberOfFiles();
        return result;
    }

    public async Task<(CrystalObjectResult<TData> Result, Waypoint Waypoint, string Path)> LoadLatest<TData>(PrepareParam param, SaveFormat formatHint, TData? singletonData)
        where TData : class, ITinyhandSerializable<TData>, ITinyhandReconstructable<TData>
    {
        if (this.main is null)
        {
            return (new(CrystalResult.NotPrepared), Waypoint.Invalid, string.Empty);
        }

        if (!this.IsProtected)
        {// Not protected (no file history)
            var result = await this.main.LoadLatest<TData>(param, formatHint, singletonData).ConfigureAwait(false);
            if (result.Result.IsFailure && this.backup is not null)
            {// Load backup
                result = await this.backup.LoadLatest<TData>(param, formatHint, singletonData).ConfigureAwait(false);
                if (result.Result.IsSuccess)
                {// Backup restored
                    this.logger.TryGet(LogLevel.Warning)?.Log(string.Format(HashedString.Get(CrystalDataHashed.CrystalFiler.BackupLoaded), result.Path));
                }
            }

            return result;
        }
        else
        {// Protected
            if (this.backup is not null)
            {
                var mainLatest = this.main.GetLatestWaypoint();
                var backupLatest = this.backup.GetLatestWaypoint();
                if (backupLatest > mainLatest)
                {// Backup ahead
                    var resultBackup = await this.backup.LoadLatest<TData>(param, formatHint, singletonData).ConfigureAwait(false);
                    if (resultBackup.Result.IsSuccess)
                    {
                        if (await param.Query.LoadBackup(resultBackup.Path).ConfigureAwait(false) == UserInterface.YesOrNo.Yes)
                        {
                            return resultBackup;
                        }
                    }
                }
            }

            var result = await this.main.LoadLatest<TData>(param, formatHint, singletonData).ConfigureAwait(false);
            return result;
        }
    }

    public async Task<CrystalResult> DeleteAllAsync()
    {
        if (this.main is null)
        {
            return CrystalResult.NotPrepared;
        }

        var result = await this.main.DeleteAllAsync().ConfigureAwait(false);
        _ = this.backup?.DeleteAllAsync();
        return result;
    }
}
