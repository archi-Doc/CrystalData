// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace xUnitTest.CrystalDataTest;

public class SimpleJournalTest
{
    private const int DefaultCompleteBookLength = 1024 * 1024 * 16;

    [Fact]
    public async Task MissingBookKeepsPositionAfterFollowingBooks()
    {
        var directory = Directory.CreateTempSubdirectory("CrystalData-Journal-").FullName;
        try
        {
            var control = await Start(directory, DefaultCompleteBookLength);
            var journal = control.Journal!;
            var bookEnds = new ulong[4];
            for (var i = 0; i < bookEnds.Length; i++)
            {// Books with different lengths: 40, 80, 120, 160 bytes.
                AddWaypoints(journal, 10 * (i + 1));
                Assert.Equal(CrystalResult.Success, await journal.StoreData(cancellationToken: TestContext.Current.CancellationToken));
                bookEnds[i] = journal.GetCurrentPosition();
            }

            await control.StoreAndRip(TestContext.Current.CancellationToken);
            var files = GetBookFiles(directory);
            Assert.Equal(bookEnds.Length, files.Length);
            File.Delete(files[1]); // Gap between the first and the third book.

            control = await Start(directory, DefaultCompleteBookLength);
            journal = control.Journal!;
            Assert.Equal(bookEnds[^1], journal.GetCurrentPosition());
            await AssertReadable(journal, bookEnds[1], bookEnds[^1]);
            await control.StoreAndRip(TestContext.Current.CancellationToken);
        }
        finally
        {
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task MergedBooksStayContiguousAcrossRestart()
    {
        const int completeBookLength = 1_000;
        const int numberOfBooks = 40;
        var directory = Directory.CreateTempSubdirectory("CrystalData-Journal-").FullName;
        try
        {
            var control = await Start(directory, completeBookLength);
            var journal = control.Journal!;
            var start = journal.GetCurrentPosition();
            for (var i = 0; i < numberOfBooks; i++)
            {// Books of 40 and 120 bytes, so that merged ranges are not multiples of one book length.
                AddWaypoints(journal, i % 2 == 0 ? 10 : 30);
                Assert.Equal(CrystalResult.Success, await journal.StoreData(cancellationToken: TestContext.Current.CancellationToken));
            }

            var end = journal.GetCurrentPosition();
            Assert.Equal((ulong)(numberOfBooks / 2 * 160), end - start);
            await AssertReadable(journal, start, end);
            await control.StoreAndRip(TestContext.Current.CancellationToken);

            var files = GetBookFiles(directory);
            Assert.InRange(files.Length, 1, numberOfBooks - 1); // Books were merged.
            Assert.Equal((long)(end - start), files.Sum(x => new FileInfo(x).Length)); // No unused merged book is left.

            control = await Start(directory, completeBookLength);
            journal = control.Journal!;
            Assert.Equal(end, journal.GetCurrentPosition());
            await AssertReadable(journal, start, end);
            await control.StoreAndRip(TestContext.Current.CancellationToken);
        }
        finally
        {
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task CorruptedBookIsNotRead()
    {
        var directory = Directory.CreateTempSubdirectory("CrystalData-Journal-").FullName;
        try
        {
            var control = await Start(directory, DefaultCompleteBookLength);
            var journal = control.Journal!;
            var start = journal.GetCurrentPosition();
            AddWaypoints(journal, 10);
            Assert.Equal(CrystalResult.Success, await journal.StoreData(cancellationToken: TestContext.Current.CancellationToken));
            await control.StoreAndRip(TestContext.Current.CancellationToken);

            var file = Assert.Single(GetBookFiles(directory));
            var bytes = await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken);
            bytes[1] ^= 0xFF; // Same length, different content.
            await File.WriteAllBytesAsync(file, bytes, TestContext.Current.CancellationToken);

            control = await Start(directory, DefaultCompleteBookLength);
            var result = await control.Journal!.ReadJournalAsync(start);
            using var data = result.Data;
            Assert.Equal(0ul, result.NextPosition);
            await control.StoreAndRip(TestContext.Current.CancellationToken);
        }
        finally
        {
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task MergeSkipsBookThatCannotBeMerged()
    {
        const int completeBookLength = 1_000;
        var directory = Directory.CreateTempSubdirectory("CrystalData-Journal-").FullName;
        try
        {
            var control = await Start(directory, completeBookLength);
            var journal = control.Journal!;
            var start = journal.GetCurrentPosition();
            for (var i = 0; i < 22; i++)
            {// Two books of 600 bytes (which cannot be merged together), then books of 40 bytes.
                AddWaypoints(journal, i < 2 ? 150 : 10);
                Assert.Equal(CrystalResult.Success, await journal.StoreData(cancellationToken: TestContext.Current.CancellationToken));
            }

            var end = journal.GetCurrentPosition();
            await AssertReadable(journal, start, end);
            await control.StoreAndRip(TestContext.Current.CancellationToken);

            var files = GetBookFiles(directory);
            Assert.InRange(files.Length, 1, 10); // The books after the first one are merged (22 books if not).
            Assert.Equal((long)(end - start), files.Sum(x => new FileInfo(x).Length));

            control = await Start(directory, completeBookLength);
            Assert.Equal(end, control.Journal!.GetCurrentPosition());
            await AssertReadable(control.Journal!, start, end);
            await control.StoreAndRip(TestContext.Current.CancellationToken);
        }
        finally
        {
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    private static void AddWaypoints(IJournal journal, int count)
    {
        for (var i = 0; i < count; i++)
        {
            journal.AddWaypoint();
        }
    }

    private static async Task AssertReadable(IJournal journal, ulong start, ulong end)
    {
        var position = start;
        while (position != end)
        {
            var result = await journal.ReadJournalAsync(position);
            using var data = result.Data;
            Assert.InRange(result.NextPosition, position + 1, end);
            Assert.Equal((int)(result.NextPosition - position), data.Memory.Length);
            position = result.NextPosition;
        }
    }

    private static string[] GetBookFiles(string directory)
        => Directory.GetFiles(Path.Combine(directory, "journal")).Order(StringComparer.Ordinal).ToArray();

    private static async Task<CrystalControl> Start(string directory, int completeBookLength)
    {
        var product = new CrystalUnit.Builder().ConfigureCrystal(context =>
        {
            context.SetCrystalOptions(new CrystalOptions { DataDirectory = directory, });
            context.SetJournal(new TestJournalConfiguration(Path.Combine(directory, "journal"), completeBookLength));
        }).Build();
        var control = product.Context.ServiceProvider.GetRequiredService<CrystalControl>();
        Assert.Equal(CrystalResult.Success, await control.PrepareAndLoad(false));
        return control;
    }

    private sealed record TestJournalConfiguration : SimpleJournalConfiguration
    {
        public TestJournalConfiguration(string directory, int completeBookLength)
            : base(new LocalDirectoryConfiguration(directory), saveIntervalInMilliseconds: int.MaxValue)
        {
            this.CompleteBookLength = completeBookLength;
        }
    }
}
