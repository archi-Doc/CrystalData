// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Arc.Collections;
using CrystalData;
using CrystalData.Filer;
using Microsoft.Extensions.DependencyInjection;
using Tinyhand;
using ValueLink;
using Xunit;

namespace xUnitTest.CrystalDataTest;

[TinyhandObject]
public partial class PersistenceData
{
    [Key(0)]
    public int Value { get; set; }
}

[TinyhandObject]
public partial class SingletonData
{
    [Key(0)]
    public int Value { get; set; }
}

[TinyhandObject(Structural = true)]
public partial class InlinePointsData
{
    [Key(0, PropertyName = "First", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    private StoragePoint<PersistenceData> first = new();

    [Key(1, PropertyName = "Second", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    private StoragePoint<PersistenceData> second = new();
}

public class PersistenceTest
{
    [Fact]
    public async Task ConcurrentGetOrCreateReturnsOneRegisteredCrystal()
    {
        await using var scope = new PersistenceScope();
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => scope.Control.GetOrCreateCrystal<PersistenceData>(scope.Configuration), TestContext.Current.CancellationToken)));

        Assert.All(results, crystal => Assert.Same(results[0], crystal));
        Assert.Same(results[0], scope.Control.GetCrystal<PersistenceData>());
        results[0].Data.Value = 123;
        Assert.Equal(CrystalResult.Success, await results[0].StoreData(StoreMode.ForceRelease, TestContext.Current.CancellationToken));
        Assert.Equal(123, results[0].Data.Value);
    }

    [Fact]
    public async Task PreparationFailureIsReturnedAndRetryDoesNotSkipTheFailure()
    {
        await using var scope = new PersistenceScope(register: true);
        var parent = Path.Combine(scope.DirectoryPath, "data");
        await File.WriteAllTextAsync(parent, "Blocks directory creation", TestContext.Current.CancellationToken);

        Assert.NotEqual(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        Assert.NotEqual(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        File.Delete(parent);
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
    }

    [Fact]
    public async Task FailedStoreDoesNotReportSuccessOrWriteCleanShutdownMarker()
    {
        await using var scope = new PersistenceScope(register: true);
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        scope.Control.GetCrystal<PersistenceData>().Data.Value = 123;
        await scope.Control.Store(TestContext.Current.CancellationToken);
        scope.Control.GetCrystal<PersistenceData>().Data.Value = 124;
        File.Delete(scope.Configuration.FileConfiguration.Path);
        Directory.CreateDirectory(scope.Configuration.FileConfiguration.Path);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => scope.Control.Store(TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<IOException>(() => scope.Control.StoreAndRip(TestContext.Current.CancellationToken));
            Assert.False(File.Exists(Path.Combine(scope.DirectoryPath, "CrystalData.Supplement.Rip")));
        }
        finally
        {
            Directory.Delete(scope.Configuration.FileConfiguration.Path);
        }
    }

    [Fact]
    public async Task FailedBackupWriteIsReportedAndCanBeRetried()
    {
        await using var scope = new PersistenceScope(register: true, backup: true);
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var crystal = scope.Control.GetCrystal<PersistenceData>();
        await scope.Control.Store(TestContext.Current.CancellationToken);
        crystal.Data.Value = 456;
        var backup = scope.Configuration.BackupFileConfiguration!.Path;
        File.Delete(backup);
        Directory.CreateDirectory(backup);
        try
        {
            Assert.NotEqual(CrystalResult.Success, await crystal.StoreData(cancellationToken: TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(backup);
        }

        Assert.Equal(CrystalResult.Success, await crystal.StoreData(cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(File.Exists(backup));
        Assert.Equal(
            await File.ReadAllBytesAsync(scope.Configuration.FileConfiguration.Path, TestContext.Current.CancellationToken),
            await File.ReadAllBytesAsync(backup, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadBeyondEndDoesNotDeleteExistingData()
    {
        await using var scope = new PersistenceScope();
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var (filer, _) = await scope.Control.ResolveAndPrepareAndCheckSingleFiler<PersistenceData>(scope.Configuration.FileConfiguration);
        Assert.NotNull(filer);
        byte[] expected = [1, 2, 3, 4];
        Assert.Equal(CrystalResult.Success, await filer.WriteAsync(0, BytePool.RentedReadOnlyMemory.CreateFrom(expected)));
        var result = await filer.ReadAsync(0, expected.Length + 1);
        try
        {
            Assert.NotEqual(CrystalResult.Success, result.Result);
            Assert.Equal(expected, await File.ReadAllBytesAsync(scope.Configuration.FileConfiguration.Path, TestContext.Current.CancellationToken));
        }
        finally
        {
            result.Return();
        }
    }

    [Fact]
    public async Task ConcurrentStoresAndShorterOverwriteRoundTrip()
    {
        await using var scope = new PersistenceScope(register: true);
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var crystal = scope.Control.GetCrystal<PersistenceData>();
        crystal.Data.Value = int.MaxValue;
        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => crystal.StoreData(cancellationToken: TestContext.Current.CancellationToken), TestContext.Current.CancellationToken)));
        Assert.All(results, result => Assert.Equal(CrystalResult.Success, result));
        crystal.Data.Value = 1;
        Assert.Equal(CrystalResult.Success, await crystal.StoreData(StoreMode.ForceRelease, TestContext.Current.CancellationToken));
        Assert.Equal(1, crystal.Data.Value);
    }

    [Fact]
    public async Task PinnedOnlyStorageIsSavedWithoutLooping()
    {
        await using var scope = new PersistenceScope();
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var crystal = scope.CreateStorageCrystal();
        var point = crystal.Data;
        var data = await point.PinData();
        data.Value = 77;

        var save = ((IPersistable)scope.Control.StorageControl).StoreData(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(CrystalResult.Success, await save.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        var files = scope.GetStorageFiles();
        var file = Assert.Single(files);
        Assert.Equal(77, TinyhandSerializer.Deserialize<PersistenceData>(await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken))!.Value);

        Assert.True(await point.StoreData(StoreMode.TryRelease));
        data.Value = 78;
        Assert.Equal(CrystalResult.Success, await ((IPersistable)scope.Control.StorageControl).StoreData(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains(scope.GetStorageFiles(), path => TinyhandSerializer.Deserialize<PersistenceData>(File.ReadAllBytes(path))!.Value == 78);
    }

    [Fact]
    public async Task FailedStoragePointReleaseKeepsDataForRetry()
    {
        await using var scope = new PersistenceScope();
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var crystal = scope.CreateStorageCrystal();
        var point = crystal.Data;
        using (var dataScope = await point.TryLock())
        {
            Assert.True(dataScope.IsValid);
            dataScope.Data.Value = 88;
        }

        var directory = Path.Combine(scope.DirectoryPath, "storage");
        var movedDirectory = directory + "-moved";
        await scope.Control.Store(TestContext.Current.CancellationToken);
        using (var dataScope = await point.TryLock())
        {
            Assert.True(dataScope.IsValid);
            dataScope.Data.Value = 89;
        }

        var storageUsage = crystal.Storage.StorageUsage;
        Directory.Move(directory, movedDirectory);
        await File.WriteAllTextAsync(directory, "Blocks storage writes", TestContext.Current.CancellationToken);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => point.StoreData(StoreMode.TryRelease));
            await Assert.ThrowsAsync<IOException>(() => point.StoreData(StoreMode.TryRelease));
            Assert.Equal(89, (await point.TryGet())!.Value);
            Assert.Equal(storageUsage, crystal.Storage.StorageUsage); // Failed writes do not leak file entries.
        }
        finally
        {
            File.Delete(directory);
            Directory.Move(movedDirectory, directory);
        }

        Assert.True(await point.StoreData(StoreMode.TryRelease));
        Assert.Equal(89, (await point.TryGet())!.Value);
        Assert.Equal(storageUsage, crystal.Storage.StorageUsage); // The old file is replaced.
    }

    [Fact]
    public async Task StoragePointRejectsDeserializableDataWithInvalidHash()
    {
        await using var scope = new PersistenceScope();
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var point = scope.CreateStorageCrystal().Data;
        using (var dataScope = await point.TryLock())
        {
            Assert.True(dataScope.IsValid);
            dataScope.Data.Value = 11;
        }

        Assert.True(await point.StoreData(StoreMode.TryRelease));
        var file = Assert.Single(scope.GetStorageFiles());
        await File.WriteAllBytesAsync(file, TinyhandSerializer.Serialize(new PersistenceData { Value = 22, }), TestContext.Current.CancellationToken);
        Assert.Null(await point.TryGet());
    }

    [Fact]
    public async Task StoreWaitsForStoragePointMutationLock()
    {
        await using var scope = new PersistenceScope();
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var point = scope.CreateStorageCrystal().Data;
        Task<bool> store;
        using (var dataScope = await point.TryLock(AcquisitionMode.GetOrCreate))
        {
            Assert.True(dataScope.IsValid);
            dataScope.Data.Value = 10;
            store = point.StoreData(StoreMode.StoreOnly);
            Assert.False(store.IsCompleted);
            dataScope.Data.Value = 20;
        }

        Assert.True(await store.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.True(await point.StoreData(StoreMode.TryRelease));
        Assert.Equal(20, (await point.TryGet())!.Value);
    }

    [Fact]
    public async Task SupplementFailureDoesNotWriteCleanShutdownMarker()
    {
        await using var scope = new PersistenceScope();
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var supplement = Path.Combine(scope.DirectoryPath, "CrystalData.Supplement");
        Directory.CreateDirectory(supplement);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => scope.Control.StoreAndRip(TestContext.Current.CancellationToken));
            Assert.False(File.Exists(supplement + ".Rip"));
        }
        finally
        {
            Directory.Delete(supplement);
        }
    }

    [Fact]
    public async Task FailedFactoryDoesNotLeaveStoragePointLocked()
    {
        var point = new StoragePoint<PersistenceData>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => point.TryLock(factory: _ => throw new InvalidOperationException("Factory failure")).AsTask());
        using var dataScope = await point.TryLock(AcquisitionMode.GetOrCreate, TimeSpan.Zero, TestContext.Current.CancellationToken);
        Assert.True(dataScope.IsValid);
    }

    [Fact]
    public async Task FailedAcquisitionReleasesProtectionState()
    {
        var point = new StoragePoint<PersistenceData>();
        using (var missing = await point.TryLock(AcquisitionMode.GetOnly))
        {
            Assert.False(missing.IsValid);
        }

        using (var created = await point.TryLock(AcquisitionMode.CreateOnly))
        {
            Assert.True(created.IsValid);
        }

        using (var duplicate = await point.TryLock(AcquisitionMode.CreateOnly))
        {
            Assert.False(duplicate.IsValid);
        }

        using var retrieved = await point.TryLock(AcquisitionMode.GetOnly, TimeSpan.Zero, TestContext.Current.CancellationToken);
        Assert.True(retrieved.IsValid);
    }

    [Fact]
    public async Task InitialWriteFailureIsRetriedEvenWhenDataIsUnchanged()
    {
        await using var scope = new PersistenceScope(register: true);
        Directory.CreateDirectory(scope.Configuration.FileConfiguration.Path);
        try
        {
            Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
            await Assert.ThrowsAsync<IOException>(() => scope.Control.Store(TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(scope.Configuration.FileConfiguration.Path);
        }

        await scope.Control.Store(TestContext.Current.CancellationToken);
        Assert.True(File.Exists(scope.Configuration.FileConfiguration.Path));
    }

    [Fact]
    public async Task CorruptedLatestSnapshotFallsBackToMatchingBackup()
    {
        await using var scope = new PersistenceScope(register: true, backup: true, histories: 1);
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var crystal = scope.Control.GetCrystal<PersistenceData>();
        crystal.Data.Value = 123;
        Assert.Equal(CrystalResult.Success, await crystal.StoreData(StoreMode.ForceRelease, TestContext.Current.CancellationToken));
        var file = Assert.Single(Directory.GetFiles(Path.Combine(scope.DirectoryPath, "data"), "*.tinyhand"));
        await File.WriteAllBytesAsync(file, TinyhandSerializer.SerializeToUtf8(new PersistenceData { Value = 456, }), TestContext.Current.CancellationToken);
        Assert.Equal(123, crystal.Data.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedJournalWriteCanBeRetried(bool failBackup)
    {
        await using var scope = new PersistenceScope(journal: true, backup: failBackup);
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var journal = scope.Control.Journal!;
        var start = journal.GetCurrentPosition();
        var end = journal.AddWaypoint();
        var directory = Path.Combine(scope.DirectoryPath, failBackup ? "journal-backup" : "journal");
        var movedDirectory = directory + "-moved";
        Directory.Move(directory, movedDirectory);
        await File.WriteAllTextAsync(directory, "Blocks journal writes", TestContext.Current.CancellationToken);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => scope.Control.Store(TestContext.Current.CancellationToken));
            Assert.False(File.Exists(Path.Combine(scope.DirectoryPath, "CrystalData.Supplement.Rip")));
        }
        finally
        {
            File.Delete(directory);
            Directory.Move(movedDirectory, directory);
        }

        await scope.Control.Store(TestContext.Current.CancellationToken);
        Assert.NotEmpty(Directory.GetFiles(directory));
        var result = await journal.ReadJournalAsync(start);
        using var memory = result.Data;
        Assert.Equal(end, result.NextPosition);
        Assert.Equal(4, memory.Length);
    }

    [Fact]
    public async Task EvictedJournalRemainsReadable()
    {
        await using var scope = new PersistenceScope(journal: true, evictJournal: true);
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var journal = scope.Control.Journal!;
        var start = journal.GetCurrentPosition();
        var end = journal.AddWaypoint();
        await scope.Control.Store(TestContext.Current.CancellationToken);
        Assert.Equal(start, journal.GetStartingPosition());
        var result = await journal.ReadJournalAsync(start);
        using var memory = result.Data;
        Assert.Equal(end, result.NextPosition);
        Assert.Equal(4, memory.Length);
    }

    [Fact]
    public async Task ConcurrentJournalAppendAndStorePreserveAllRecords()
    {
        await using var scope = new PersistenceScope(journal: true);
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var journal = scope.Control.Journal!;
        var start = journal.GetCurrentPosition();
        var writers = Enumerable.Range(0, 3).Select(_ => Task.Run(
            () =>
            {
                for (var i = 0; i < 100_000; i++)
                {
                    journal.AddWaypoint();
                }
            },
            TestContext.Current.CancellationToken)).ToArray();
        var stores = Task.Run(
            async () =>
            {
                for (var i = 0; i < 20; i++)
                {
                    Assert.Equal(CrystalResult.Success, await journal.StoreData(cancellationToken: TestContext.Current.CancellationToken));
                }
            },
            TestContext.Current.CancellationToken);
        await Task.WhenAll(writers.Append(stores)).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await scope.Control.Store(TestContext.Current.CancellationToken);
        var result = await journal.ReadJournalAsync(start);
        using var memory = result.Data;
        Assert.Equal(start + 1_200_000, result.NextPosition);
        Assert.Equal(1_200_000, memory.Length);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SnapshotAheadOfLostJournalSurvivesUnchangedRestart(int histories)
    {
        var directory = Directory.CreateTempSubdirectory("CrystalData-Persistence-").FullName;
        try
        {
            var configuration = new CrystalConfiguration(new LocalFileConfiguration(Path.Combine(directory, "data", "value.tinyhand")))
            {
                SaveFormat = SaveFormat.Utf8,
                NumberOfHistoryFiles = histories,
            };

            var control = await StartControl<PersistenceData>(directory, configuration, true);
            control.GetCrystal<PersistenceData>().Data.Value = 1;
            await control.Store(TestContext.Current.CancellationToken); // An older snapshot, also ahead of the journal.
            control.GetCrystal<PersistenceData>().Data.Value = 123;
            await control.StoreAndRip(TestContext.Current.CancellationToken);

            // The journal restarts behind the stored snapshot, so loading moves the snapshot back to the journal position.
            Directory.Delete(Path.Combine(directory, "journal"), true);
            control = await StartControl<PersistenceData>(directory, configuration, true);
            Assert.Equal(123, control.GetCrystal<PersistenceData>().Data.Value);
            await control.StoreAndRip(TestContext.Current.CancellationToken); // Unchanged data

            control = await StartControl<PersistenceData>(directory, configuration, true);
            Assert.Equal(123, control.GetCrystal<PersistenceData>().Data.Value);
            await control.StoreAndRip(TestContext.Current.CancellationToken);
        }
        finally
        {
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    [Theory]
    [InlineData("./data/value.tinyhand")]
    [InlineData("data/../data/value.tinyhand")]
    public async Task HistoryFilesAreFoundForUnnormalizedPath(string file)
    {
        var directory = Directory.CreateTempSubdirectory("CrystalData-Persistence-").FullName;
        try
        {
            var configuration = new CrystalConfiguration(new LocalFileConfiguration(file))
            {
                SaveFormat = SaveFormat.Utf8,
                NumberOfHistoryFiles = 2,
            };

            var control = await StartControl<PersistenceData>(directory, configuration, false);
            control.GetCrystal<PersistenceData>().Data.Value = 123;
            await control.StoreAndRip(TestContext.Current.CancellationToken);

            control = await StartControl<PersistenceData>(directory, configuration, false);
            Assert.Equal(123, control.GetCrystal<PersistenceData>().Data.Value);
            await control.StoreAndRip(TestContext.Current.CancellationToken);
            Assert.Equal(2, Directory.GetFiles(Path.Combine(directory, "data")).Length); // Old history files are also found and limited.
        }
        finally
        {
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task FilerTimeoutIsReturnedAsResult()
    {
        await using var scope = new PersistenceScope();
        using var slowFiler = new SlowFiler(scope.Control.Root);
        IFiler filer = slowFiler;
        var timeout = TimeSpan.FromMilliseconds(10);

        Assert.Equal(CrystalResult.Aborted, await filer.WriteAsync("write", 0, BytePool.RentedReadOnlyMemory.CreateFrom([1]), timeout));
        Assert.Equal(CrystalResult.Aborted, await filer.DeleteAsync("delete", timeout));
        Assert.Equal(CrystalResult.Aborted, await filer.DeleteDirectoryAsync("directory", true, timeout));
        var read = await filer.ReadAsync("read", 0, 1, timeout);
        Assert.Equal(CrystalResult.Aborted, read.Result);
        Assert.Empty(await filer.ListAsync("list", timeout));
    }

    [Fact]
    public async Task InlineStoragePointIsKeptWhenReleaseIsBlocked()
    {
        await using var scope = new PersistenceScope();
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var crystal = scope.Control.CreateCrystal<InlinePointsData>(scope.Configuration); // Storage disabled: points are serialized inline.
        Assert.Equal(CrystalResult.Success, await crystal.PrepareAndLoad(false));
        using (var first = await crystal.Data.First.TryLock())
        {
            first.Data!.Value = 1;
        }

        using (var second = await crystal.Data.Second.TryLock())
        {// The first point is stored, then the release fails at the locked second point, so the crystal is not saved.
            Assert.True(second.IsValid);
            Assert.Equal(CrystalResult.DataIsLocked, await crystal.StoreData(StoreMode.TryRelease, TestContext.Current.CancellationToken));
        }

        Assert.Equal(1, (await crystal.Data.First.TryGet())?.Value);
        Assert.Equal(CrystalResult.Success, await crystal.StoreData(StoreMode.ForceRelease, TestContext.Current.CancellationToken));
        Assert.Equal(1, (await crystal.Data.First.TryGet())?.Value); // Reloaded from the file.
    }

    [Fact]
    public async Task InlineStoragePointMovedToStorageIsReleasedFromMemoryUsage()
    {
        await using var scope = new PersistenceScope();
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var crystal = scope.Control.CreateCrystal<InlinePointsData>(scope.Configuration); // Storage disabled: points are serialized inline.
        Assert.Equal(CrystalResult.Success, await crystal.PrepareAndLoad(false));
        using (var first = await crystal.Data.First.TryLock())
        {
            first.Data!.Value = 1;
        }

        Assert.Equal(CrystalResult.Success, await crystal.StoreData(StoreMode.ForceRelease, TestContext.Current.CancellationToken));

        // Storage is enabled, so the inline data moves to the storage on the next save.
        crystal.ConfigureStorage(new SimpleStorageConfiguration(new LocalDirectoryConfiguration(Path.Combine(scope.DirectoryPath, "storage"))));
        Assert.Equal(CrystalResult.Success, await crystal.PrepareAndLoad(false));
        Assert.Equal(CrystalResult.Success, await crystal.StoreData(StoreMode.StoreOnly, TestContext.Current.CancellationToken));
        Assert.True(scope.Control.StorageControl.MemoryUsage > 0);
        Assert.Equal(CrystalResult.Success, await crystal.StoreData(StoreMode.ForceRelease, TestContext.Current.CancellationToken));
        Assert.Equal(0, scope.Control.StorageControl.MemoryUsage);
        Assert.Equal(1, (await crystal.Data.First.TryGet())?.Value); // Loaded from the storage.
    }

    [Fact]
    public async Task StoragePointIsRestoredFromJournalWhenOtherPointIsCreated()
    {
        await using var scope = new PersistenceScope(journal: true);
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var crystal = scope.Control.CreateCrystal<InlinePointsData>(scope.Configuration with
        {
            StorageConfiguration = new SimpleStorageConfiguration(new LocalDirectoryConfiguration(Path.Combine(scope.DirectoryPath, "storage")))
            {
                NumberOfHistoryFiles = 2,
            },
        });
        Assert.Equal(CrystalResult.Success, await crystal.PrepareAndLoad(false));
        var first = crystal.Data.First;
        first.Set(new PersistenceData { Value = 1, });
        Assert.True(await first.StoreData(StoreMode.StoreOnly));
        first.Set(new PersistenceData { Value = 2, });
        using (var second = await crystal.Data.Second.TryLock())
        {// A record of the storage map (not of a point) in the journal to be restored.
            Assert.True(second.IsValid);
        }

        Assert.True(await first.StoreData(StoreMode.StoreOnly));
        await scope.Control.StoreJournal();

        // The latest storage is lost, so the data is restored from the previous storage and the journal.
        first.DeleteLatestStorageForTest();
        Assert.True(await first.StoreData(StoreMode.ForceRelease));
        Assert.Equal(2, (await first.TryGet())?.Value);
    }

    [Fact]
    public async Task StorageMapIsSavedWhilePointsAreCreated()
    {
        await using var scope = new PersistenceScope();
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var crystal = scope.Control.CreateCrystal<CreditData.GoshujinClass>(scope.Configuration with
        {
            StorageConfiguration = new SimpleStorageConfiguration(new LocalDirectoryConfiguration(Path.Combine(scope.DirectoryPath, "storage"))),
        });
        Assert.Equal(CrystalResult.Success, await crystal.PrepareAndLoad(false));
        var storage = (IPersistable)crystal.Storage;
        var creating = Task.Run(
            async () =>
            {
                for (var i = 0; i < 2_000; i++)
                {
                    CreditData creditData;
                    using (var w = crystal.Data.TryLock(i, AcquisitionMode.GetOrCreate)!)
                    {
                        creditData = w.Commit()!;
                    }

                    using (var borrowers = await creditData.Borrowers.TryLock())
                    {// Adds a storage object to the storage map.
                        Assert.True(borrowers.IsValid);
                    }
                }
            },
            TestContext.Current.CancellationToken);

        while (!creating.IsCompleted)
        {// The map is serialized while storage objects are added.
            Assert.Equal(CrystalResult.Success, await storage.StoreData(cancellationToken: TestContext.Current.CancellationToken));
        }

        await creating;
        Assert.Equal(CrystalResult.Success, await storage.StoreData(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteDirectoryResolvesRelativePathAgainstDataDirectory()
    {
        await using var scope = new PersistenceScope();
        var subdirectory = Path.Combine(scope.DirectoryPath, "sub");
        Directory.CreateDirectory(subdirectory);
        await File.WriteAllTextAsync(Path.Combine(subdirectory, "file"), "data", TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, new LocalDirectoryConfiguration(string.Empty).Path); // Not "/" (the root directory).
        scope.Control.DeleteDirectory(new LocalDirectoryConfiguration(string.Empty));
        scope.Control.DeleteDirectory(new LocalDirectoryConfiguration());
        scope.Control.DeleteDirectory(new LocalDirectoryConfiguration("."));
        scope.Control.DeleteDirectory(new LocalDirectoryConfiguration("sub/.."));
        scope.Control.DeleteDirectory(new LocalDirectoryConfiguration(".."));
        Assert.True(Directory.Exists(subdirectory)); // DataDirectory itself is not deleted.

        scope.Control.DeleteDirectory(new LocalDirectoryConfiguration("sub"));
        Assert.False(Directory.Exists(subdirectory));
        Assert.True(Directory.Exists(scope.DirectoryPath));
    }

    [Fact]
    public async Task SharedConfigurationDoesNotMakeOtherCrystalsSingletons()
    {
        var directory = Directory.CreateTempSubdirectory("CrystalData-Persistence-").FullName;
        try
        {// A directory path, so each type gets its own file.
            var configuration = new CrystalConfiguration(new LocalFileConfiguration("data/")) { SaveFormat = SaveFormat.Utf8, };
            var product = new CrystalUnit.Builder().ConfigureCrystal((unitContext, context) =>
            {
                unitContext.Services.AddSingleton<SingletonData>();
                context.SetCrystalOptions(new CrystalOptions { DataDirectory = directory, });
                context.AddCrystal<SingletonData>(configuration);
                context.AddCrystal<PersistenceData>(configuration);
            }).Build();
            var control = product.Context.ServiceProvider.GetRequiredService<CrystalControl>();

            // PersistenceData treated as a singleton would resolve itself while loading, which deadlocks.
            Assert.Equal(CrystalResult.Success, await control.PrepareAndLoad(false).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            var singleton = product.Context.ServiceProvider.GetRequiredService<SingletonData>();
            Assert.Same(singleton, control.GetCrystal<SingletonData>().Data);
            control.GetCrystal<PersistenceData>().Data.Value = 1;

            var crystal = control.GetCrystal<SingletonData>();
            Assert.Equal(CrystalResult.Success, await crystal.StoreData(StoreMode.ForceRelease, TestContext.Current.CancellationToken));
            crystal.Configure(configuration); // As LoadConfigurations() does; the configuration does not have the singleton flag.
            Assert.Equal(CrystalResult.Success, await crystal.PrepareAndLoad(false));
            Assert.Same(singleton, crystal.Data);
            await control.StoreAndRip(TestContext.Current.CancellationToken);
        }
        finally
        {
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task StoragePointRestoredFromJournalIsStored()
    {
        var directory = Directory.CreateTempSubdirectory("CrystalData-Persistence-").FullName;
        try
        {
            var configuration = new CrystalConfiguration(new LocalFileConfiguration(Path.Combine(directory, "data", "point.tinyhand")))
            {
                SaveFormat = SaveFormat.Utf8,
                StorageConfiguration = new SimpleStorageConfiguration(new LocalDirectoryConfiguration(Path.Combine(directory, "storage"))),
            };

            // The latest value is only in the journal (crash without storing).
            var control = await StartControl<StoragePoint<PersistenceData>>(directory, configuration, true);
            var point = control.GetCrystal<StoragePoint<PersistenceData>>().Data;
            point.Set(new PersistenceData { Value = 1, });
            await control.Store(TestContext.Current.CancellationToken);
            point.Set(new PersistenceData { Value = 2, });
            await control.StoreJournal();
            Assert.True(await control.Root.WaitForTerminationAsync(TimeSpan.FromSeconds(10), cancellationToken: TestContext.Current.CancellationToken));

            // The journal is replayed. The point is not accessed, so only the replayed data can be stored.
            control = await StartControl<StoragePoint<PersistenceData>>(directory, configuration, true);
            await control.StoreAndRip(TestContext.Current.CancellationToken);

            // The journal is not replayed after a clean shutdown.
            control = await StartControl<StoragePoint<PersistenceData>>(directory, configuration, true);
            Assert.Equal(2, (await control.GetCrystal<StoragePoint<PersistenceData>>().Data.TryGet())?.Value);
            await control.StoreAndRip(TestContext.Current.CancellationToken);
        }
        finally
        {
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task DeleteRemovesLoadedFileWithoutHistory()
    {
        var directory = Directory.CreateTempSubdirectory("CrystalData-Persistence-").FullName;
        try
        {
            var path = Path.Combine(directory, "data", "value.tinyhand");
            var configuration = new CrystalConfiguration(new LocalFileConfiguration(path))
            {
                SaveFormat = SaveFormat.Utf8,
                NumberOfHistoryFiles = 0,
            };

            var control = await StartControl<PersistenceData>(directory, configuration, false);
            control.GetCrystal<PersistenceData>().Data.Value = 123;
            await control.StoreAndRip(TestContext.Current.CancellationToken);
            Assert.True(File.Exists(path));

            control = await StartControl<PersistenceData>(directory, configuration, false);
            var crystal = control.GetCrystal<PersistenceData>();
            Assert.Equal(123, crystal.Data.Value); // Loaded from the file, so no waypoint is listed.
            Assert.Equal(CrystalResult.Success, await crystal.Delete());
            Assert.False(File.Exists(path));
            await control.StoreAndRip(TestContext.Current.CancellationToken);

            control = await StartControl<PersistenceData>(directory, configuration, false);
            Assert.Equal(0, control.GetCrystal<PersistenceData>().Data.Value);
            await control.StoreAndRip(TestContext.Current.CancellationToken);
        }
        finally
        {
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ReconfiguredShorterSaveIntervalTakesEffect()
    {
        await using var scope = new PersistenceScope();
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var crystal = scope.Control.CreateCrystal<PersistenceData>(scope.Configuration with { SaveInterval = TimeSpan.FromHours(1), });
        crystal.Configure(scope.Configuration with { SaveInterval = TimeSpan.FromSeconds(1), }); // Clamped to the 5-second minimum.
        Assert.Equal(CrystalResult.Success, await crystal.PrepareAndLoad(false));
        crystal.Data.Value = 123;

        var path = scope.Configuration.FileConfiguration.Path;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (await TryReadValue(path) != 123)
        {// Only the periodic save can write the new value.
            await Task.Delay(100, timeout.Token);
        }

        static async Task<int?> TryReadValue(string path)
        {
            try
            {
                return TinyhandSerializer.DeserializeFromUtf8<PersistenceData>(await File.ReadAllBytesAsync(path))?.Value;
            }
            catch (Exception ex) when (ex is IOException or TinyhandException)
            {// Not written yet, or being written.
                return null;
            }
        }
    }

    private static async Task<CrystalControl> StartControl<TData>(string directory, CrystalConfiguration configuration, bool journal)
        where TData : class, ITinyhandSerializable<TData>, ITinyhandReconstructable<TData>
    {
        var product = new CrystalUnit.Builder().ConfigureCrystal(context =>
        {
            context.SetCrystalOptions(new CrystalOptions { DataDirectory = directory, });
            if (journal)
            {
                context.SetJournal(new SimpleJournalConfiguration(new LocalDirectoryConfiguration(Path.Combine(directory, "journal"))));
            }

            context.AddCrystal<TData>(configuration);
        }).Build();
        var control = product.Context.ServiceProvider.GetRequiredService<CrystalControl>();
        Assert.Equal(CrystalResult.Success, await control.PrepareAndLoad(false));
        return control;
    }

    private sealed class SlowFiler : FilerBase
    {
        public SlowFiler(Arc.Threading.ExecutionRoot root)
            : base(root)
        {
        }

        protected override Task ProcessJobAsync(FilerWork work, CancellationToken cancellationToken)
            => Task.Delay(Timeout.Infinite, cancellationToken); // Until the filer is disposed.
    }

    private sealed record TestJournalConfiguration : SimpleJournalConfiguration
    {
        public TestJournalConfiguration(string directory, bool backup, bool evict)
            : base(new LocalDirectoryConfiguration(Path.Combine(directory, "journal")), saveIntervalInMilliseconds: int.MaxValue)
        {
            this.BackupDirectoryConfiguration = backup ? new LocalDirectoryConfiguration(Path.Combine(directory, "journal-backup")) : null;
            if (evict)
            {
                this.MaxMemoryCapacity = 0;
            }
        }
    }

    private sealed class PersistenceScope : IAsyncDisposable
    {
        public PersistenceScope(bool register = false, bool backup = false, int histories = 0, bool journal = false, bool evictJournal = false)
        {
            this.DirectoryPath = Directory.CreateTempSubdirectory("CrystalData-Persistence-").FullName;
            this.Configuration = new CrystalConfiguration(new LocalFileConfiguration(Path.Combine(this.DirectoryPath, "data", "value.tinyhand")))
            {
                SaveFormat = SaveFormat.Utf8,
                NumberOfHistoryFiles = histories,
                BackupFileConfiguration = backup ? new LocalFileConfiguration(Path.Combine(this.DirectoryPath, "backup.tinyhand")) : null,
            };
            var product = new CrystalUnit.Builder().ConfigureCrystal(context =>
            {
                context.SetCrystalOptions(new CrystalOptions { DataDirectory = this.DirectoryPath, });
                if (journal)
                {
                    context.SetJournal(new TestJournalConfiguration(this.DirectoryPath, backup, evictJournal));
                }

                if (register)
                {
                    context.AddCrystal<PersistenceData>(this.Configuration);
                }
            }).Build();
            this.Control = product.Context.ServiceProvider.GetRequiredService<CrystalControl>();
        }

        public string DirectoryPath { get; }

        public CrystalConfiguration Configuration { get; }

        public CrystalControl Control { get; }

        public ICrystal<StoragePoint<PersistenceData>> CreateStorageCrystal()
            => this.Control.CreateCrystal<StoragePoint<PersistenceData>>(this.Configuration with
            {
                StorageConfiguration = new SimpleStorageConfiguration(new LocalDirectoryConfiguration(Path.Combine(this.DirectoryPath, "storage"))),
            });

        public string[] GetStorageFiles()
            => Directory.GetFiles(Path.Combine(this.DirectoryPath, "storage"), "*", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path).Length == 6 && !Path.HasExtension(path)).ToArray();

        public async ValueTask DisposeAsync()
        {
            await this.Control.StoreAndRip();
            Directory.Delete(this.DirectoryPath, true);
        }
    }
}
