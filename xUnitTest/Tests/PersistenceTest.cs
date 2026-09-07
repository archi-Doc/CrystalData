// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Arc.Collections;
using CrystalData;
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
        Assert.Equal(CrystalResult.Success, await filer.WriteAsync(0, BytePool.RentReadOnlyMemory.CreateFrom(expected)));
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
        var point = scope.CreateStorageCrystal().Data;
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

        Directory.Move(directory, movedDirectory);
        await File.WriteAllTextAsync(directory, "Blocks storage writes", TestContext.Current.CancellationToken);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => point.StoreData(StoreMode.TryRelease));
            Assert.Equal(89, (await point.TryGet())!.Value);
        }
        finally
        {
            File.Delete(directory);
            Directory.Move(movedDirectory, directory);
        }

        Assert.True(await point.StoreData(StoreMode.TryRelease));
        Assert.Equal(89, (await point.TryGet())!.Value);
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
                NumberOfFileHistories = histories,
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
