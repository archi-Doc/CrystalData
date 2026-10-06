// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Arc.Collections;
using CrystalData;
using CrystalData.Storage;
using Tinyhand;
using Tinyhand.IO;
using ValueLink;
using Xunit;

namespace xUnitTest.CrystalDataTest;

public class StorageIntegrityTest
{
    [Fact]
    public async Task GetOnlyIgnoreStateDoesNotCreateMissingData()
    {
        var point = new StoragePoint<PersistenceData>();
        using var scope = await point.TryLock(AcquisitionMode.GetOnlyIgnoreState);
        Assert.Equal(DataScopeResult.NotFound, scope.Result);
        Assert.Null(await point.TryGet());
    }

    [Fact]
    public async Task DirectDeletionCannotResurrectThePoint()
    {
        var point = new StoragePoint<PersistenceData>();
        point.Set(new() { Value = 123 });
        await point.DeleteData();
        Assert.True(point.IsDeleted);
        Assert.Null(await point.TryGet());
        using var scope = await point.TryLock();
        Assert.Equal(DataScopeResult.Obsolete, scope.Result);
        point.Set(new() { Value = 456 });
        Assert.Null(await point.TryGet());
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await point.PinData());
    }

    [Fact]
    public async Task DeletionWaitsForTheActiveScopeAndThenPreventsAcquisition()
    {
        var point = new StoragePoint<PersistenceData>();
        var scope = await point.TryLock();
        var deletion = point.DeleteData();
        Assert.False(deletion.IsCompleted);
        scope.Dispose();
        await deletion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using var after = await point.TryLock();
        Assert.Equal(DataScopeResult.Obsolete, after.Result);
    }

    [Fact]
    public async Task DeserializingAnIdentifierInvalidatesCachedInlineData()
    {
        StoragePoint<PersistenceData>? point = new();
        point.Set(new() { Value = 123 });
        TinyhandSerializer.DeserializeObject(TinyhandSerializer.Serialize(0ul), ref point);
        Assert.NotNull(point);
        Assert.Null(await point.TryGet());
    }

    [Fact]
    public async Task ConcurrentFirstAcquisitionUsesOneDataInstance()
    {
        var point = new StoragePoint<PersistenceData>();
        var results = await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(
            async () =>
            {
                using var scope = await point.TryLock();
                Assert.True(scope.IsValid);
                scope.Data.Value++;
                return scope.Data;
            },
            TestContext.Current.CancellationToken)));
        Assert.All(results, data => Assert.Same(results[0], data));
        Assert.Equal(64, results[0].Value);
    }

    [Fact]
    public void StorageIndexReplayIsIdempotent()
    {
        var data = new SimpleStorageData();
        var record = CreateFileRecord(123, 20, 20);
        Replay(data, record);
        Replay(data, record);
        Assert.Equal(1, data.Count);
        Assert.Equal(20, data.StorageUsage);
        record = CreateFileRecord(123, 7, -13);
        Replay(data, record);
        Replay(data, record);
        Assert.Equal(7, data.StorageUsage);
        Assert.True(data.Remove(123));
        Assert.Equal(0, data.StorageUsage);
    }

    [Fact]
    public void InvalidStorageIndexDoesNotReplaceExistingEntries()
    {
        SimpleStorageData? data = new();
        uint file = 0;
        data.Put(ref file, 10);
        var writer = TinyhandWriter.CreateFromBytePool();
        byte[] invalid;
        try
        {
            writer.Write(999L);
            writer.WriteMapHeader(1);
            writer.Write(123u);
            writer.Write(20);
            invalid = writer.FlushAndGetArray();
        }
        finally
        {
            writer.Dispose();
        }

        Assert.Throws<TinyhandException>(() => TinyhandSerializer.DeserializeObject(invalid, ref data));
        Assert.NotNull(data);
        Assert.True(data.TryGetValue(file, out var size));
        Assert.Equal(10, size);
        Assert.Equal(10, data.StorageUsage);
    }

    [Fact]
    public void NegativeSizesDoNotChangeStorageIndex()
    {
        var data = new SimpleStorageData();
        uint file = 0;
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Put(ref file, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => data.NewFileInternal(-1));
        Assert.Equal(0u, file);
        Assert.Equal(0, data.Count);
        Assert.Equal(0, data.StorageUsage);
    }

    [Fact]
    public async Task ConcurrentFileCreationTracksEveryEntryAndByte()
    {
        var data = new SimpleStorageData();
        var files = await Task.WhenAll(Enumerable.Range(0, 128).Select(_ => Task.Run(() => data.NewFileInternal(10), TestContext.Current.CancellationToken)));
        Assert.Equal(128, files.Distinct().Count());
        Assert.DoesNotContain(0u, files);
        Assert.Equal(128, data.Count);
        Assert.Equal(1280, data.StorageUsage);
        Assert.True(data.EqualsForTest(TinyhandSerializer.Deserialize<SimpleStorageData>(TinyhandSerializer.Serialize(data))));
    }

    [Fact]
    public async Task AggregateUsageMatchesBackingStorage()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<StoragePoint<PersistenceData>>(true);
        try
        {
            using (var scope = await crystal.Data.TryLock())
            {
                Assert.True(scope.IsValid);
                scope.Data.Value = 123;
            }

            Assert.True(await crystal.Data.StoreData(StoreMode.StoreOnly));
            Assert.True(crystal.Storage.StorageUsage > 0);
            Assert.Equal(crystal.Storage.StorageUsage, crystal.CrystalControl.StorageControl.StorageUsage);
        }
        finally
        {
            await TestHelper.StoreAndReleaseAndDelete(crystal);
        }
    }

    [Fact]
    public async Task DeletingUnloadedParentAlsoDeletesStoredChildren()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<StoragePoint<InlinePointsData>>(true);
        try
        {
            using (var parent = await crystal.Data.TryLock())
            {
                Assert.True(parent.IsValid);
                parent.Data.First.Set(new() { Value = 123 });
                parent.Data.Second.Set(new() { Value = 456 });
            }

            Assert.True(await crystal.Data.StoreData(StoreMode.ForceRelease));
            Assert.True(crystal.Storage.StorageUsage > 0);
            await crystal.Data.DeleteData();
            Assert.Equal(0, crystal.Storage.StorageUsage);
        }
        finally
        {
            await TestHelper.StoreAndReleaseAndDelete(crystal);
        }
    }

    [Fact]
    public async Task UnpinningRestoresMemoryAccounting()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<StoragePoint<PersistenceData>>(true);
        try
        {
            long usage;
            using (var scope = await crystal.Data.TryLock())
            {
                Assert.True(scope.IsValid);
                usage = crystal.CrystalControl.StorageControl.MemoryUsage;
                Assert.True(usage > 0);
                scope.SetControlState(DataControlState.Pinned);
                Assert.Equal(0, crystal.CrystalControl.StorageControl.MemoryUsage);
            }

            using (var scope = await crystal.Data.TryLock())
            {
                Assert.True(scope.IsValid);
                scope.SetControlState(DataControlState.None);
                Assert.Equal(usage, crystal.CrystalControl.StorageControl.MemoryUsage);
            }
        }
        finally
        {
            await TestHelper.StoreAndReleaseAndDelete(crystal);
        }
    }

    [Fact]
    public async Task CancelledStorageReleaseDoesNotReportSuccess()
    {
        IPersistable control = new StorageControl();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => control.StoreData(StoreMode.ForceRelease, cancellation.Token));
    }

    [Fact]
    public void StorageIdParsingRequiresOneCompleteIdentifier()
    {
        var expected = new StorageId(1, 2, 3);
        Assert.True(StorageId.TryParse(expected.ToBase32(), out var actual));
        Assert.Equal(expected, actual);
        Assert.False(StorageId.TryParse(expected.ToBase32() + "00", out _));
        Assert.False(StorageId.TryParse("!" + expected.ToBase32()[1..], out _));
        Assert.False(StorageId.TryParse(string.Empty, out _));
        Assert.False(StorageId.TryParse(null!, out _));
    }

    [Fact]
    public async Task RecoveryRejectsMalformedMatchingRecord()
    {
        var bytes = CreateJournalRecord(malformed: true);
        IJournal journal = new MemoryJournal(bytes);
        Assert.Null(await journal.RestoreData(1, 0, new SptClass2(), 1, 2));
    }

    [Fact]
    public async Task RecoveryRejectsTruncatedRecord()
    {
        var bytes = CreateJournalRecord(malformed: false);
        bytes[2] += 10; // The payload is shorter than the declared record length.
        IJournal journal = new MemoryJournal(bytes);
        Assert.Null(await journal.RestoreData(1, 0, new SptClass2(), 1, 2));
    }

    [Fact]
    public async Task RecoveryRequiresTheEntireJournalRange()
    {
        var bytes = CreateJournalRecord(malformed: false);
        IJournal journal = new MemoryJournal(bytes);
        Assert.Null(await journal.RestoreData(1, (ulong)bytes.Length + 2, new SptClass2(), 1, 2));
    }

    [Fact]
    public async Task RecoveryStopsAtUpperBoundAndIgnoresOtherPoints()
    {
        var bytes = CreateJournalRecord(malformed: false);
        IJournal journal = new MemoryJournal(bytes);
        var original = new SptClass2();
        Assert.Same(original, await journal.RestoreData(1, 1, original, 1, 2));
        Assert.Same(original, await journal.RestoreData(1, 0, original, 1, 3));
        Assert.Equal(123, Assert.IsType<SptClass2>(await journal.RestoreData(1, 0, original, 1, 2)).Count);
    }

    private static byte[] CreateJournalRecord(bool malformed)
    {
        var writer = TinyhandWriter.CreateFromBytePool();
        try
        {
            writer.WriteLocatorRecord();
            writer.Write(1u);
            writer.WriteLocatorRecord();
            writer.Write(2ul);
            writer.WriteKeyRecord();
            writer.Write(1); // SptClass2.Count
            if (!malformed)
            {
                writer.WriteValueRecord();
                writer.Write(123);
            }

            var payload = writer.FlushAndGetArray();
            var bytes = new byte[payload.Length + 4];
            bytes[0] = (byte)(payload.Length >> 16);
            bytes[1] = (byte)(payload.Length >> 8);
            bytes[2] = (byte)payload.Length;
            bytes[3] = (byte)JournalType.Record;
            payload.CopyTo(bytes, 4);
            return bytes;
        }
        finally
        {
            writer.Dispose();
        }
    }

    private static byte[] CreateFileRecord(uint file, int size, int delta)
    {
        var writer = TinyhandWriter.CreateFromBytePool();
        try
        {
            writer.Write(JournalRecordType.AddItem);
            writer.Write(file);
            writer.Write(size);
            writer.Write(delta);
            return writer.FlushAndGetArray();
        }
        finally
        {
            writer.Dispose();
        }
    }

    private static void Replay(SimpleStorageData data, byte[] record)
    {
        var reader = new TinyhandReader(record);
        Assert.True(((ITinyhandCustomJournal)data).ReadCustomRecord(ref reader));
    }

    private sealed class MemoryJournal(byte[] bytes) : IJournal
    {
        public int MaxRecordLength => int.MaxValue;

        public Type DataType => typeof(MemoryJournal);

        public Task<CrystalResult> Prepare(PrepareParam param) => Task.FromResult(CrystalResult.Success);

        public void GetWriter(JournalType recordType, out TinyhandWriter writer) => throw new NotSupportedException();

        public ulong Add(ref TinyhandWriter writer) => throw new NotSupportedException();

        public ulong AddWaypoint() => throw new NotSupportedException();

        public ulong GetStartingPosition() => 1;

        public ulong GetCurrentPosition() => (ulong)bytes.Length + 1;

        public void ResetJournal(ulong position) => throw new NotSupportedException();

        public Task<(ulong NextPosition, BytePool.RentedMemory Data)> ReadJournalAsync(ulong position)
        {
            if (position != 1)
            {
                return Task.FromResult((0ul, default(BytePool.RentedMemory)));
            }

            var memory = BytePool.Default.Rent(bytes.Length).AsMemory(0, bytes.Length);
            bytes.CopyTo(memory.Span);
            return Task.FromResult((this.GetCurrentPosition(), memory));
        }

        public Task Terminate() => Task.CompletedTask;

        public Task<CrystalResult> StoreData(StoreMode storeMode, CancellationToken cancellationToken) => Task.FromResult(CrystalResult.Success);

        public Task<bool> TestJournal() => Task.FromResult(true);
    }
}
