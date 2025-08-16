// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.IO;
using System.Runtime.CompilerServices;
using CrystalData.Filer;

#pragma warning disable SA1124 // Do not use regions
#pragma warning disable SA1204

namespace CrystalData.Storage;

internal partial class SimpleStorage : IStorage
{
    private const string Filename = "Simple";

    public SimpleStorage(Crystalizer crystalizer)
    {
        this.crystalizer = crystalizer;
        this.timeout = TimeSpan.MinValue;
        this.storageMap = crystalizer.StorageControl.DisabledMap;
    }

    public override string ToString()
        => $"SimpleStorage {StorageHelper.ByteToString(this.StorageUsage)}";

    #region FieldAndProperty

    private Crystalizer crystalizer;
    private string directory = string.Empty;
    private string backupDirectory = string.Empty;
    private ICrystal<SimpleStorageData>? storageCrystal;
    private ICrystal<StorageMap>? mapCrystal;
    private StorageMap storageMap;
    private IRawFiler? mainFiler;
    private IRawFiler? backupFiler;
    private TimeSpan timeout;

    public StorageMap StorageMap => this.storageMap;

    public long StorageUsage => this.storageData == null ? 0 : this.storageData.StorageUsage;

    Type IPersistable.DataType => typeof(SimpleStorage);

    private SimpleStorageData? storageData => this.storageCrystal?.Data;

    #endregion

    #region IStorage

    void IStorage.SetTimeout(TimeSpan timeout)
    {
        this.timeout = timeout;
    }

    async Task<CrystalResult> IStorage.PrepareAndCheck(PrepareParam param, StorageConfiguration storageConfiguration)
    {
        CrystalResult result;
        var directoryConfiguration = storageConfiguration.DirectoryConfiguration;

        if (string.IsNullOrEmpty(this.directory))
        {
            this.directory = directoryConfiguration.Path;
            if (!string.IsNullOrEmpty(this.directory) && !this.directory.EndsWith('/'))
            {
                this.directory += "/";
            }
        }

        if (this.mainFiler is null)
        {
            (this.mainFiler, directoryConfiguration) = this.crystalizer.ResolveRawFiler(directoryConfiguration);
            result = await this.mainFiler.PrepareAndCheck(param, directoryConfiguration).ConfigureAwait(false);
            if (result.IsFailure())
            {
                return result;
            }
        }

        // Backup
        var backupDirectoryConfiguration = storageConfiguration.BackupDirectoryConfiguration;
        if (backupDirectoryConfiguration is not null)
        {
            this.backupDirectory = backupDirectoryConfiguration.Path;
            if (!string.IsNullOrEmpty(this.backupDirectory) && !this.backupDirectory.EndsWith('/'))
            {
                this.backupDirectory += "/";
            }

            if (this.backupFiler is null)
            {
                (this.backupFiler, backupDirectoryConfiguration) = this.crystalizer.ResolveRawFiler(backupDirectoryConfiguration);
                result = await this.backupFiler.PrepareAndCheck(param, backupDirectoryConfiguration).ConfigureAwait(false);
                if (result.IsFailure())
                {
                    return result;
                }
            }
        }

        if (this.storageCrystal == null)
        {// SimpleStorageData
            this.storageCrystal = this.crystalizer.CreateCrystal<SimpleStorageData>(null, false);
            var mainConfiguration = directoryConfiguration.CombineFile(Filename);
            var backupConfiguration = backupDirectoryConfiguration?.CombineFile(Filename);
            this.storageCrystal.Configure(new CrystalConfiguration(SavePolicy.Manual, mainConfiguration)
            {
                BackupFileConfiguration = backupConfiguration,
                NumberOfFileHistories = storageConfiguration.NumberOfHistoryFiles,
            });

            result = await this.storageCrystal.PrepareAndLoad(param.UseQuery).ConfigureAwait(false);
            if (result.IsFailure())
            {
                return result;
            }
        }

        if (this.mapCrystal == null)
        {// StorageMap
            this.mapCrystal = this.crystalizer.CreateCrystal<StorageMap>(null, false);
            var mainConfiguration = directoryConfiguration.CombineFile(StorageMap.Filename);
            var backupConfiguration = backupDirectoryConfiguration?.CombineFile(StorageMap.Filename);
            this.mapCrystal.Configure(new CrystalConfiguration(SavePolicy.Manual, mainConfiguration)
            {
                BackupFileConfiguration = backupConfiguration,
                NumberOfFileHistories = storageConfiguration.NumberOfHistoryFiles,
            });

            ((ICrystalInternal)this.mapCrystal).SetStorage(this);
            result = await this.mapCrystal.PrepareAndLoad(param.UseQuery).ConfigureAwait(false);
            if (result.IsFailure())
            {
                return result;
            }
            else
            {
                this.storageMap = this.mapCrystal.Data;
            }
        }

        return CrystalResult.Success;
    }

    async Task<CrystalResult> IPersistable.Store(StoreMode storeMode, CancellationToken cancellationToken)
    {
        if (this.storageCrystal is not null)
        {
            await this.storageCrystal.Store(StoreMode.StoreOnly).ConfigureAwait(false);
        }

        if (this.mapCrystal is not null)
        {
            await this.mapCrystal.Store(StoreMode.StoreOnly).ConfigureAwait(false);
        }

        return CrystalResult.Success;
    }

    /*async Task IStorageInternal.PersistStorage(ICrystal? callingCrystal)
    {
        if (!StorageHelper.CheckPrimaryCrystal(ref this.primaryCrystal, ref callingCrystal))
        {
            return;
        }

        if (this.storageCrystal is not null)
        {
            await this.storageCrystal.Store(StoreMode.StoreOnly).ConfigureAwait(false);
        }

        if (this.mapCrystal is not null)
        {
            await this.mapCrystal.Store(StoreMode.StoreOnly).ConfigureAwait(false);
        }
    }*/

    CrystalResult IStorage.PutAndForget(ref ulong fileId, BytePool.RentReadOnlyMemory dataToBeShared)
    {
        if (this.mainFiler == null || this.storageData == null)
        {
            return CrystalResult.NotPrepared;
        }

        var file = FileIdToFile(fileId);
        this.storageData.Put(ref file, dataToBeShared.Memory.Length);
        fileId = FileToFileId(file);

        var path = this.FileToPath(FileIdToFile(fileId));
        var result = this.mainFiler.WriteAndForget(this.MainFile(path), 0, dataToBeShared);
        if (this.backupFiler is not null)
        {
            this.backupFiler.WriteAndForget(this.BackupFile(path), 0, dataToBeShared);
        }

        return result;
    }

    CrystalResult IStorage.DeleteAndForget(ref ulong fileId)
    {
        if (this.mainFiler == null)
        {
            return CrystalResult.NotPrepared;
        }

        var file = FileIdToFile(fileId);
        if (file == 0)
        {
            return CrystalResult.NotFound;
        }

        if (this.storageData != null)
        {
            if (!this.storageData.Remove(file))
            {// Not found
                fileId = 0;
                return CrystalResult.NotFound;
            }

            // this.dictionary.Add(file, -1);
        }

        fileId = 0;

        var path = this.FileToPath(file);
        var result = this.mainFiler.DeleteAndForget(this.MainFile(path));
        if (this.backupFiler is not null)
        {
            this.backupFiler.DeleteAndForget(this.BackupFile(path));
        }

        return result;
    }

    Task<CrystalMemoryOwnerResult> IStorage.GetAsync(ref ulong fileId)
    {
        if (this.mainFiler == null || this.storageData == null)
        {
            return Task.FromResult(new CrystalMemoryOwnerResult(CrystalResult.NotPrepared));
        }

        var file = FileIdToFile(fileId);
        int size;
        if (!this.storageData.TryGetValue(file, out size))
        {// Not found
            fileId = 0;
            return Task.FromResult(new CrystalMemoryOwnerResult(CrystalResult.NotFound));
        }

        return ReadTask();

        async Task<CrystalMemoryOwnerResult> ReadTask()
        {
            var result = await this.mainFiler.ReadAsync(this.MainFile(this.FileToPath(file)), 0, size, this.timeout).ConfigureAwait(false);
            if (result.IsFailure && this.backupFiler is not null)
            {
                result = await this.backupFiler.ReadAsync(this.BackupFile(this.FileToPath(file)), 0, size, this.timeout).ConfigureAwait(false);
            }

            return result;
        }
    }

    Task<CrystalResult> IStorage.PutAsync(ref ulong fileId, BytePool.RentReadOnlyMemory dataToBeShared)
    {
        if (this.mainFiler == null || this.storageData == null)
        {
            return Task.FromResult(CrystalResult.NotPrepared);
        }

        var file = FileIdToFile(fileId);
        this.storageData.Put(ref file, dataToBeShared.Memory.Length);
        fileId = FileToFileId(file);

        var path = this.FileToPath(FileIdToFile(fileId));
        var task = this.mainFiler.WriteAsync(this.MainFile(path), 0, dataToBeShared, this.timeout);
        if (this.backupFiler is not null)
        {
            _ = this.backupFiler.WriteAsync(this.BackupFile(path), 0, dataToBeShared, this.timeout);
        }

        return task;
    }

    Task<CrystalResult> IStorage.DeleteAsync(ref ulong fileId)
    {
        if (this.mainFiler == null || this.storageData == null)
        {
            return Task.FromResult(CrystalResult.NotPrepared);
        }

        var file = FileIdToFile(fileId);
        if (file == 0)
        {
            return Task.FromResult(CrystalResult.NotFound);
        }

        this.storageData.Remove(file);

        fileId = 0;

        var path = this.FileToPath(FileIdToFile(fileId));
        var task = this.mainFiler.DeleteAsync(this.MainFile(path), this.timeout);
        if (this.backupFiler is not null)
        {
            _ = this.backupFiler.DeleteAsync(this.BackupFile(path), this.timeout);
        }

        return task;
    }

    async Task<CrystalResult> IStorage.DeleteStorageAsync()
    {
        if (this.mainFiler == null)
        {
            return CrystalResult.NotPrepared;
        }

        // Method 1: Delete the files in Storage one by one. If the Storage folder is not empty, leave it intact.
        /*if (this.storageCrystal is { } crystal)
        {
            var array = crystal.Data.GetFileArray();
            foreach (var x in array)
            {
                var path = this.FileToPath(x);
                this.mainFiler.DeleteAndForget(this.MainFile(path));
                if (this.backupFiler is not null)
                {
                    this.backupFiler.DeleteAndForget(this.BackupFile(path));
                }
            }

            await crystal.Delete().ConfigureAwait(false);
        }

        if (this.mapCrystal is { } crystal2)
        {
            await crystal2.Delete().ConfigureAwait(false);
        }

        _ = this.backupFiler?.DeleteDirectoryAsync(this.backupDirectory, false).ConfigureAwait(false);
        return await this.mainFiler.DeleteDirectoryAsync(this.directory, false).ConfigureAwait(false);*/

        // Method 2: Delete the Storage folder entirely.
        _ = this.backupFiler?.DeleteDirectoryAsync(this.backupDirectory, true).ConfigureAwait(false);
        return await this.mainFiler.DeleteDirectoryAsync(this.directory, true).ConfigureAwait(false);
    }

    async Task<bool> IPersistable.TestJournal()
    {
        if (this.storageCrystal is ICrystalInternal crystalInternal &&
            await crystalInternal.TestJournal().ConfigureAwait(false) == false)
        {
            return false;
        }

        if (this.mapCrystal is ICrystalInternal crystalInternal2 &&
            await crystalInternal2.TestJournal().ConfigureAwait(false) == false)
        {
            return false;
        }

        return true;
    }

    #endregion

    #region Helper

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint FileIdToFile(ulong fileId) => (uint)(fileId >> 32);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong FileToFileId(uint file) => (ulong)file << 32;

    private string FileToPath(uint file)
    {
        Span<char> c = stackalloc char[9];
        var n = 0;

        c[n++] = UInt32ToChar(file >> 28);
        c[n++] = UInt32ToChar(file >> 24);

        c[n++] = '/';

        c[n++] = UInt32ToChar(file >> 20);
        c[n++] = UInt32ToChar(file >> 16);
        c[n++] = UInt32ToChar(file >> 12);
        c[n++] = UInt32ToChar(file >> 8);
        c[n++] = UInt32ToChar(file >> 4);
        c[n++] = UInt32ToChar(file);

        return c.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char UInt32ToChar(uint x)
    {
        var a = x & 0xF;
        if (a < 10)
        {
            return (char)('0' + a);
        }
        else
        {
            return (char)('W' + a);
        }
    }

    private string MainFile(string path) => this.directory + path;

    private string BackupFile(string path) => this.backupDirectory + path;

    #endregion
}
