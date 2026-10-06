// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData;
using Microsoft.Extensions.DependencyInjection;
using Tinyhand;
using Xunit;

namespace xUnitTest.CrystalDataTest;

[TinyhandObject]
public partial class ConcurrentLoadData
{
    private static int deserializationCount;

    public static int DeserializationCount
    {
        get => Volatile.Read(ref deserializationCount);
        set => Volatile.Write(ref deserializationCount, value);
    }

    public static ManualResetEventSlim? Entered { get; set; }

    public static ManualResetEventSlim? Continue { get; set; }

    [Key(0)]
    public int Value { get; set; }

    [TinyhandOnDeserialized]
    private void OnDeserialized()
    {
        Interlocked.Increment(ref deserializationCount);
        Entered?.Set();
        if (Continue is { } gate && !gate.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException();
        }
    }
}

public class CrystalLifecycleTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReconfigurationStoresUnchangedData(bool replaceWholeConfiguration)
    {
        await using var scope = new Scope();
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var crystal = scope.Control.CreateCrystal<PersistenceData>(scope.Configuration);
        crystal.Data.Value = 42;
        Assert.Equal(CrystalResult.Success, await crystal.StoreData(cancellationToken: TestContext.Current.CancellationToken));
        var destination = new LocalFileConfiguration(Path.Combine(scope.DirectoryPath, "moved.tinyhand"));
        if (replaceWholeConfiguration)
        {
            crystal.Configure(scope.Configuration with { FileConfiguration = destination, });
        }
        else
        {
            crystal.ConfigureFile(destination);
        }

        Assert.Equal(CrystalResult.Success, await crystal.PrepareAndLoad(false));
        Assert.Equal(CrystalResult.Success, await crystal.StoreData(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(42, TinyhandSerializer.DeserializeFromUtf8<PersistenceData>(await File.ReadAllBytesAsync(destination.Path, TestContext.Current.CancellationToken))!.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task VolatileCrystalDoesNotWriteInitialSnapshot(int histories)
    {
        await using var scope = new Scope();
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var crystal = scope.Control.CreateCrystal<PersistenceData>(scope.Configuration with { IsVolatile = true, NumberOfHistoryFiles = histories, });
        Assert.Equal(CrystalResult.Success, await crystal.PrepareAndLoad(false));
        crystal.Data.Value = 42;
        await scope.Control.Store(TestContext.Current.CancellationToken);
        Assert.Empty(Directory.GetFiles(scope.DirectoryPath, "value*"));
    }

    [Fact]
    public async Task FailedLazyLoadDoesNotConstructUnsavableDefaults()
    {
        await using var scope = new Scope();
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        var blocker = Path.Combine(scope.DirectoryPath, "blocked");
        await File.WriteAllTextAsync(blocker, "file", TestContext.Current.CancellationToken);
        var crystal = scope.Control.CreateCrystal<PersistenceData>(scope.Configuration with { FileConfiguration = new LocalFileConfiguration(Path.Combine(blocker, "value.tinyhand")), });
        Assert.Throws<IOException>(() => crystal.Data);
        Assert.NotEqual(CrystalResult.Success, await crystal.Delete());
        File.Delete(blocker);
        Assert.Equal(CrystalResult.Success, await crystal.PrepareAndLoad(false));
        crystal.Data.Value = 23;
        Assert.Equal(CrystalResult.Success, await crystal.StoreData(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SupplementPreparationFailureCanBeRetried()
    {
        await using var scope = new Scope(supplement: "blocked/supplement");
        var blocker = Path.Combine(scope.DirectoryPath, "blocked");
        await File.WriteAllTextAsync(blocker, "file", TestContext.Current.CancellationToken);
        Assert.NotEqual(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        Assert.False(scope.Control.IsPrepared);
        File.Delete(blocker);
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("1invalid", false)]
    [InlineData("-1", false)]
    public async Task ShutdownMarkerIsValidatedAndRemovedBeforeStartupCompletes(string marker, bool clean)
    {
        await using var scope = new Scope();
        var path = Path.Combine(scope.DirectoryPath, "CrystalData.Supplement.Rip");
        await File.WriteAllTextAsync(path, marker, TestContext.Current.CancellationToken);
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        Assert.Equal(clean, scope.Control.CrystalSupplement.IsRip);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task ConcurrentPreparationDeserializesOneInstance()
    {
        await using var scope = new Scope();
        Assert.Equal(CrystalResult.Success, await scope.Control.PrepareAndLoad(false));
        await File.WriteAllBytesAsync(scope.Configuration.FileConfiguration.Path, TinyhandSerializer.SerializeToUtf8(new ConcurrentLoadData { Value = 42, }), TestContext.Current.CancellationToken);
        var crystal = scope.Control.CreateCrystal<ConcurrentLoadData>(scope.Configuration);
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        ConcurrentLoadData.DeserializationCount = 0;
        ConcurrentLoadData.Entered = entered;
        ConcurrentLoadData.Continue = resume;
        var first = Task.Run(() => crystal.PrepareAndLoad(false), TestContext.Current.CancellationToken);
        Task<CrystalResult>? second = null;
        Task<CrystalResult>? store = null;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            second = Task.Run(() => crystal.PrepareAndLoad(false), TestContext.Current.CancellationToken);
            store = crystal.StoreData(cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(store.IsCompleted);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            resume.Set();
            Assert.All(await Task.WhenAll(first, second, store).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken), result => Assert.Equal(CrystalResult.Success, result));
            Assert.Equal(1, ConcurrentLoadData.DeserializationCount);
            Assert.Equal(42, crystal.Data.Value);
        }
        finally
        {
            resume.Set();
            await first;
            if (second is not null)
            {
                await second;
            }

            if (store is not null)
            {
                await store;
            }

            ConcurrentLoadData.Entered = null;
            ConcurrentLoadData.Continue = null;
        }
    }

    private sealed class Scope : IAsyncDisposable
    {
        public Scope(string? supplement = null)
        {
            this.DirectoryPath = Directory.CreateTempSubdirectory("CrystalData-Lifecycle-").FullName;
            this.Configuration = new(new LocalFileConfiguration(Path.Combine(this.DirectoryPath, "value.tinyhand"))) { SaveFormat = SaveFormat.Utf8, NumberOfHistoryFiles = 0, };
            var product = new CrystalUnit.Builder().ConfigureCrystal(context => context.SetCrystalOptions(new CrystalOptions
            {
                DataDirectory = this.DirectoryPath,
                SupplementFile = supplement is null ? null : new LocalFileConfiguration(supplement),
            })).Build();
            this.Control = product.Context.ServiceProvider.GetRequiredService<CrystalControl>();
        }

        public string DirectoryPath { get; }

        public CrystalConfiguration Configuration { get; }

        public CrystalControl Control { get; }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await this.Control.StoreAndRip(TestContext.Current.CancellationToken);
            }
            finally
            {
                TestHelper.TryDeleteDirectory(this.DirectoryPath);
            }
        }
    }
}
