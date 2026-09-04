// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Buffers.Binary;
using CrystalData;
using CrystalData.Filer;
using CrystalData.Storage;
using Tinyhand;
using Xunit;

namespace xUnitTest.CrystalDataTest;

public class UtilityTest
{
    [Fact]
    public void StorageIdRoundTripsEveryField()
    {
        var expected = new StorageId(0x0102_0304_0506_0708, 0x1112_1314_1516_1718, 0x2122_2324_2526_2728);

        var bytes = expected.ToByteArray();
        Assert.Equal(StorageId.Length, bytes.Length);
        Assert.Equal(expected.JournalPosition, BinaryPrimitives.ReadUInt64BigEndian(bytes));
        Assert.Equal(expected.FileId, BitConverter.ToUInt64(bytes.AsSpan(sizeof(ulong))));
        Assert.Equal(expected.Hash, BitConverter.ToUInt64(bytes.AsSpan(sizeof(ulong) * 2)));

        Assert.True(StorageId.TryParse(bytes, out var fromBytes));
        Assert.Equal(expected, fromBytes);
        Assert.True(StorageId.TryParse(expected.ToBase32(), out var fromBase32));
        Assert.Equal(expected, fromBase32);
        Assert.False(StorageId.TryParse("!", out _));
    }

    [Fact]
    public void WaypointTryParseRejectsInvalidText()
    {
        var expected = new Waypoint(123, 456, 789);

        Assert.True(Waypoint.TryParse(expected.ToBase32(), out var actual));
        Assert.Equal(expected, actual);
        Assert.False(Waypoint.TryParse("!", out _));
    }

    [Fact]
    public void ReadOnlyMemoryStreamImplementsStreamBounds()
    {
        using var stream = new ReadOnlyMemoryStream(new byte[] { 1, 2, 3, 4, });
        var destination = new byte[4];

        Assert.Equal(2, stream.Read(destination, 1, 2));
        Assert.Equal(new byte[] { 0, 1, 2, 0, }, destination);
        Assert.Equal(1, stream.Seek(-1, SeekOrigin.Current));
        Assert.Equal(2, stream.ReadByte());
        Assert.Equal(4, stream.Seek(0, SeekOrigin.End));
        Assert.Equal(-1, stream.ReadByte());
        stream.Flush();

        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Equal(4, stream.Length);

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = (long)int.MaxValue + 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Seek(-1, SeekOrigin.Begin));
        stream.Position = 1;
        Assert.Throws<IOException>(() => stream.Seek(long.MaxValue, SeekOrigin.Current));
        Assert.Throws<ArgumentException>(() => stream.Seek(0, (SeekOrigin)int.MaxValue));
        Assert.Throws<ArgumentNullException>(() => stream.Read(null!, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Read(destination, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Read(destination, 0, destination.Length + 1));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(1));
        Assert.Throws<NotSupportedException>(() => stream.Write(destination, 0, 1));
    }

    [Fact]
    public void PathSplitPreservesRootDirectory()
    {
        var root = Path.GetPathRoot(Environment.CurrentDirectory)!;

        Assert.Equal((root, string.Empty), StorageHelper.PathToDirectoryAndFile(root));
        Assert.Equal((root, "prefix"), StorageHelper.PathToDirectoryAndFile(Path.Combine(root, "prefix")));
        Assert.Equal(("directory", "file"), StorageHelper.PathToDirectoryAndFile("directory/file"));
        Assert.Equal((string.Empty, "file"), StorageHelper.PathToDirectoryAndFile("file"));
    }

    [Fact]
    public void StorageHelperFormatsAndCombinesPaths()
    {
        Assert.Equal("999B", StorageHelper.ByteToString(999));
        Assert.Equal("1.0K", StorageHelper.ByteToString(1_000));
        Assert.Equal("10K", StorageHelper.ByteToString(10_000));
        Assert.Equal("1.0M", StorageHelper.ByteToString(1_000_000));
        Assert.Equal("9.2E", StorageHelper.ByteToString(long.MaxValue));

        Assert.Equal("right", StorageHelper.CombineWithSlash(string.Empty, "right"));
        Assert.Equal("left", StorageHelper.CombineWithSlash("left", string.Empty));
        Assert.Equal("left/right", StorageHelper.CombineWithSlash("left/", "/right"));
        Assert.Equal("left/right", StorageHelper.CombineWithSlash("left/", "right"));
        Assert.Equal("left/right", StorageHelper.CombineWithSlash("left", "/right"));
        Assert.Equal("left/right", StorageHelper.CombineWithSlash("left", "right"));
        Assert.Equal("left\\right", StorageHelper.CombineWithBackslash("left", "right"));

        Assert.True(StorageHelper.EndsWith_SlashInsensitive("root\\folder/file", "folder\\file"));
        Assert.False(StorageHelper.EndsWith_SlashInsensitive("file", "folder/file"));
        Assert.False(StorageHelper.EndsWith_SlashInsensitive("root/file", "root/other"));
        Assert.True(StorageHelper.EndsWithSlashOrBackslash("root/"));
        Assert.True(StorageHelper.IsSeparator(':'));
        Assert.False(StorageHelper.IsSeparator('|'));
    }

    [Fact]
    public void AccessKeyParserPreservesSeparatorsInSecret()
    {
        Assert.False(AccessKeyPair.TryParse(null!, out _));
        Assert.True(AccessKeyPair.TryParse("access=secret=with=padding", out var pair));
        Assert.Equal("access", pair.AccessKeyId);
        Assert.Equal("secret=with=padding", pair.SecretAccessKey);

        Assert.True(AccessKeyPair.TryParse("bucket=access=secret=with=padding", out var bucket, out pair));
        Assert.Equal("bucket", bucket);
        Assert.Equal("access", pair.AccessKeyId);
        Assert.Equal("secret=with=padding", pair.SecretAccessKey);
        Assert.False(AccessKeyPair.TryParse("missing-separator", out pair));
        Assert.False(AccessKeyPair.TryParse(null!, out _, out _));
        Assert.False(AccessKeyPair.TryParse("only=one", out _, out _));
    }

    [Fact]
    public void MonoDataEnforcesCapacityWhenReduced()
    {
        var data = new MonoData<int, string>(3);
        data.Set(1, "one");
        data.Set(2, "two");
        data.Set(3, "three");
        data.Set(1, "updated");
        data.Set(4, "four");

        Assert.False(data.TryGet(2, out _));
        Assert.True(data.TryGet(1, out var updated));
        Assert.Equal("updated", updated);

        data.SetCapacity(1);
        Assert.Equal(1, data.Count);
        Assert.True(data.TryGet(4, out var latest));
        Assert.Equal("four", latest);
        Assert.False(data.TryGet(1, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => data.SetCapacity(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonoData<int, string>(-1));

        var serialized = TinyhandSerializer.Serialize(data);
        var restored = TinyhandSerializer.Deserialize<MonoData<int, string>>(serialized);
        Assert.NotNull(restored);
        Assert.Equal(1, restored.Capacity);
        Assert.True(restored.TryGet(4, out latest));
        Assert.Equal("four", latest);
        restored.Set(5, "five");
        Assert.False(restored.TryGet(4, out _));
        Assert.True(restored.TryGet(5, out _));
    }

    [Fact]
    public async Task EmptyStorageProvidesPersistableMetadata()
    {
        var persistable = (IPersistable)EmptyStorage.Default;

        Assert.Equal(typeof(EmptyStorage), persistable.DataType);
        Assert.Equal(CrystalResult.Success, await persistable.StoreData(cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(await persistable.TestJournal());
    }
}
