// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Buffers.Binary;
using Arc.Crypto;
using CrystalData;
using CrystalData.Journal;
using Microsoft.Extensions.DependencyInjection;
using Tinyhand;
using Xunit;

namespace xUnitTest.CrystalDataTest;

public class JournalRangeRegressionTest
{
    [Fact]
    public async Task BufferedRecordsAreReadableBeforeSaving()
    {
        await using var scope = await JournalScope.Start();
        var journal = scope.Control.Journal!;
        var start = journal.GetCurrentPosition();
        var end = journal.AddWaypoint();
        var result = await journal.ReadJournalAsync(start);
        using var data = result.Data;
        Assert.Equal(end, result.NextPosition);
        Assert.Equal(4, data.Length);

        var nextEnd = journal.AddWaypoint();
        var destination = new byte[4];
        Assert.True(await ((SimpleJournal)journal).ReadJournalAsync(end, nextEnd, destination));
        Assert.Equal(new byte[] { 0, 0, 0, (byte)JournalType.Waypoint }, destination);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestartRecoversMergeLeftoversWithoutLosingOriginalRecords(bool corruptMergedBook)
    {
        var directory = Directory.CreateTempSubdirectory("CrystalData-JournalRange-").FullName;
        try
        {
            ulong end;
            await using (var scope = await JournalScope.Start(directory))
            {
                var journal = scope.Control.Journal!;
                journal.AddWaypoint();
                Assert.Equal(CrystalResult.Success, await journal.StoreData(cancellationToken: TestContext.Current.CancellationToken));
                end = journal.AddWaypoint();
                Assert.Equal(CrystalResult.Success, await journal.StoreData(cancellationToken: TestContext.Current.CancellationToken));
            }

            var journalDirectory = Path.Combine(directory, "journal");
            var originals = Directory.GetFiles(journalDirectory).Order(StringComparer.Ordinal).ToArray();
            Assert.Equal(2, originals.Length);
            var merged = originals.SelectMany(File.ReadAllBytes).ToArray();
            var title = new byte[20];
            BinaryPrimitives.WriteUInt64BigEndian(title, 1);
            BinaryPrimitives.WriteUInt64LittleEndian(title.AsSpan(8), FarmHash.Hash64(merged));
            var mergedPath = Path.Combine(journalDirectory, Base32Sort.Default.FromBytesToString(title) + SimpleJournal.CompleteSuffix);
            var expected = merged.ToArray();
            if (corruptMergedBook)
            {
                merged[0] ^= 0xFF;
            }

            await File.WriteAllBytesAsync(mergedPath, merged, TestContext.Current.CancellationToken);
            await using (var restarted = await JournalScope.Start(directory))
            {
                var journal = restarted.Control.Journal!;
                Assert.Equal(1ul, journal.GetStartingPosition());
                Assert.Equal(end, journal.GetCurrentPosition());
                var result = await journal.ReadJournalAsync(1);
                using var data = result.Data;
                Assert.Equal(end, result.NextPosition);
                Assert.Equal(expected, data.Memory.ToArray());
            }

            Assert.Equal(corruptMergedBook ? 2 : 1, Directory.GetFiles(journalDirectory).Length);
        }
        finally
        {
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task RangeReadLeavesBytesAfterRequestedEndUnchanged()
    {
        await using var scope = await JournalScope.Start();
        var journal = scope.Control.Journal!;
        var start = journal.GetCurrentPosition();
        for (var i = 0; i < 4; i++)
        {
            journal.AddWaypoint();
        }

        Assert.Equal(CrystalResult.Success, await journal.StoreData(cancellationToken: TestContext.Current.CancellationToken));
        var destination = Enumerable.Repeat((byte)0xCC, 16).ToArray();
        Assert.True(await ((SimpleJournal)journal).ReadJournalAsync(start + 1, start + 5, destination));
        Assert.Equal(new byte[] { 0, 0, (byte)JournalType.Waypoint, 0 }, destination[..4]);
        Assert.All(destination[4..], value => Assert.Equal(0xCC, value));
    }

    [Fact]
    public async Task InvalidAndEmptyJournalRangesAreHandledWithoutOverflow()
    {
        await using var scope = await JournalScope.Start();
        var journal = (SimpleJournal)scope.Control.Journal!;
        var destination = Enumerable.Repeat((byte)0xCC, 16).ToArray();
        Assert.False(await journal.ReadJournalAsync(2, 1, destination));
        Assert.False(await journal.ReadJournalAsync(1, ulong.MaxValue, destination));
        Assert.False(await journal.ReadJournalAsync(1, (ulong)uint.MaxValue + 6, destination));
        Assert.True(await journal.ReadJournalAsync(1, 1, destination));
        Assert.All(destination, value => Assert.Equal(0xCC, value));
    }

    [Fact]
    public async Task DirectoryWithJournalSuffixIsIgnoredDuringStartup()
    {
        var directory = Directory.CreateTempSubdirectory("CrystalData-JournalRange-").FullName;
        try
        {
            ulong end;
            await using (var scope = await JournalScope.Start(directory))
            {
                end = scope.Control.Journal!.AddWaypoint();
            }

            var journalDirectory = Path.Combine(directory, "journal");
            var book = Assert.Single(Directory.GetFiles(journalDirectory));
            Directory.CreateDirectory(book[..^SimpleJournal.IncompleteSuffix.Length] + SimpleJournal.CompleteSuffix);
            await using var restarted = await JournalScope.Start(directory);
            Assert.Equal(end, restarted.Control.Journal!.GetCurrentPosition());
            var result = await restarted.Control.Journal.ReadJournalAsync(1);
            using var data = result.Data;
            Assert.Equal(end, result.NextPosition);
            Assert.Equal(4, data.Length);
        }
        finally
        {
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    private sealed class JournalScope : IAsyncDisposable
    {
        private readonly string directory;
        private readonly bool ownsDirectory;

        private JournalScope(string directory, bool ownsDirectory, CrystalControl control)
        {
            this.directory = directory;
            this.ownsDirectory = ownsDirectory;
            this.Control = control;
        }

        public CrystalControl Control { get; }

        public static async Task<JournalScope> Start(string? directory = null)
        {
            var ownsDirectory = directory is null;
            directory ??= Directory.CreateTempSubdirectory("CrystalData-JournalRange-").FullName;
            var product = new CrystalUnit.Builder().ConfigureCrystal(context =>
            {
                context.SetCrystalOptions(new CrystalOptions { DataDirectory = directory, });
                context.SetJournal(new SimpleJournalConfiguration(new LocalDirectoryConfiguration(Path.Combine(directory, "journal")), saveIntervalInMilliseconds: int.MaxValue));
            }).Build();
            var control = product.Context.ServiceProvider.GetRequiredService<CrystalControl>();
            Assert.Equal(CrystalResult.Success, await control.PrepareAndLoad(false));
            return new(directory, ownsDirectory, control);
        }

        public async ValueTask DisposeAsync()
        {
            await this.Control.StoreAndRip(TestContext.Current.CancellationToken);
            if (this.ownsDirectory)
            {
                TestHelper.TryDeleteDirectory(this.directory);
            }
        }
    }
}
