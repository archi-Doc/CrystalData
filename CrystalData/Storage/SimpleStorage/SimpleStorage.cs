// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Runtime.CompilerServices;
using CrystalData.Filer;

#pragma warning disable SA1124 // Do not use regions
#pragma warning disable SA1204

namespace CrystalData.Storage;

internal partial class SimpleStorage : IStorage, IStorageInternal
{
    private const string Filename = "Simple";

    public SimpleStorage(Crystalizer crystalizer)
    {
        this.crystalizer = crystalizer;
        this.timeout = TimeSpan.MinValue;
    }

    public override string ToString()
        => $"SimpleStorage {StorageHelper.ByteToString(this.StorageUsage)}";

    #region FieldAndProperty

    public long StorageUsage => this.data == null ? 0 : this.data.StorageUsage;

    private SimpleStorageData? data => this.crystal?.Data;

    private Crystalizer crystalizer;
    private ICrystal? primaryCrystal;
    private string directory = string.Empty;
    private string backupDirectory = string.Empty;
    private ICrystal<SimpleStorageData>? crystal;
    private IRawFiler? mainFiler;
    private IRawFiler? backupFiler;
    private TimeSpan timeout;

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

        if (this.crystal == null)
        {
            this.crystal = this.crystalizer.CreateCrystal<SimpleStorageData>(null, false);
            var mainConfiguration = directoryConfiguration.CombineFile(Filename);
            var backupConfiguration = backupDirectoryConfiguration?.CombineFile(Filename);
            this.crystal.Configure(new CrystalConfiguration(SavePolicy.Manual, mainConfiguration)
            {
                BackupFileConfiguration = backupConfiguration,
                NumberOfFileHistories = storageConfiguration.NumberOfHistoryFiles,
            });

            result = await this.crystal.PrepareAndLoad(param.UseQuery).ConfigureAwait(false);
            return result;
        }

        return CrystalResult.Success;
    }

    async Task IStorage.SaveStorage(ICrystal? callingCrystal)
    {
        if (!StorageHelper.CheckPrimaryCrystal(ref this.primaryCrystal, ref callingCrystal))
        {
            return;
        }

        if (this.crystal != null)
        {
            await this.crystal.Save().ConfigureAwait(false);
        }
    }

    CrystalResult IStorage.PutAndForget(ref ulong fileId, BytePool.RentReadOnlyMemory dataToBeShared)
    {
        if (this.mainFiler == null || this.data == null)
        {
            return CrystalResult.NotPrepared;
        }

        var file = FileIdToFile(fileId);
        this.data.Put(ref file, dataToBeShared.Memory.Length);
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

        if (this.data != null)
        {
            if (this.data.Remove(file))
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
        if (this.mainFiler == null || this.data == null)
        {
            return Task.FromResult(new CrystalMemoryOwnerResult(CrystalResult.NotPrepared));
        }

        var file = FileIdToFile(fileId);
        int size;
        if (!this.data.TryGetValue(file, out size))
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
        if (this.mainFiler == null || this.data == null)
        {
            return Task.FromResult(CrystalResult.NotPrepared);
        }

        var file = FileIdToFile(fileId);
        this.data.Put(ref file, dataToBeShared.Memory.Length);
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
        if (this.mainFiler == null || this.data == null)
        {
            return Task.FromResult(CrystalResult.NotPrepared);
        }

        var file = FileIdToFile(fileId);
        if (file == 0)
        {
            return Task.FromResult(CrystalResult.NotFound);
        }

        this.data.Remove(file);

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

        _ = this.backupFiler?.DeleteDirectoryAsync(this.backupDirectory).ConfigureAwait(false);
        return await this.mainFiler.DeleteDirectoryAsync(this.directory).ConfigureAwait(false);
    }

    async Task<bool> IStorageInternal.TestJournal()
    {
        if (this.crystal is ICrystalInternal crystalInternal)
        {
            return await crystalInternal.TestJournal().ConfigureAwait(false);
        }
        else
        {
            return true;
        }
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
