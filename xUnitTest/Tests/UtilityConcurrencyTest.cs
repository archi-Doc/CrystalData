// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData;
using CrystalData.Filer;
using Tinyhand;
using Tinyhand.IO;
using Xunit;

namespace xUnitTest.CrystalDataTest;

[TinyhandObject]
public partial class BlockingMonoKey : IEquatable<BlockingMonoKey>
{
    public static Action<int>? OnHash { get; set; }

    [Key(0)]
    public int Value { get; set; }

    public bool Equals(BlockingMonoKey? other) => this.Value == other?.Value;

    public override bool Equals(object? obj) => obj is BlockingMonoKey other && this.Equals(other);

    public override int GetHashCode()
    {
        OnHash?.Invoke(this.Value);
        return this.Value;
    }
}

public class UtilityConcurrencyTest
{
    [Fact]
    public async Task MonoDataRestoreWaitsForActiveMutation()
    {
        var replacement = new MonoData<BlockingMonoKey, string>(1);
        replacement.Set(new() { Value = 2, }, "replacement");
        var bytes = TinyhandSerializer.SerializeObject(replacement);
        var data = new MonoData<BlockingMonoKey, string>(4);
        using var mutationEntered = new ManualResetEventSlim();
        using var restoreEntered = new ManualResetEventSlim();
        using var releaseMutation = new ManualResetEventSlim();
        BlockingMonoKey.OnHash = value =>
        {
            if (value == 1)
            {
                mutationEntered.Set();
                Assert.True(releaseMutation.Wait(TimeSpan.FromSeconds(10)));
            }
            else if (value == 2)
            {
                restoreEntered.Set();
            }
        };

        Task? mutation = null;
        Task? restore = null;
        try
        {
            mutation = Task.Run(() => data.Set(new() { Value = 1, }, "before restore"), TestContext.Current.CancellationToken);
            Assert.True(mutationEntered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            restore = Task.Run(
                () =>
                {
                    var target = data;
                    TinyhandSerializer.DeserializeObject(bytes, ref target);
                    Assert.Same(data, target);
                },
                TestContext.Current.CancellationToken);
            Assert.True(restoreEntered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

            var wait = Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
            Assert.Same(wait, await Task.WhenAny(restore, wait));
        }
        finally
        {
            releaseMutation.Set();
            try
            {
                if (mutation is not null)
                {
                    await mutation.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                }

                if (restore is not null)
                {
                    await restore.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                }
            }
            finally
            {
                BlockingMonoKey.OnHash = null;
            }
        }

        Assert.Equal(1, data.Capacity);
        Assert.Equal(1, data.Count);
        Assert.False(data.TryGet(new() { Value = 1, }, out _));
        Assert.True(data.TryGet(new() { Value = 2, }, out var value));
        Assert.Equal("replacement", value);
    }

    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(2, 1, 1)]
    public void MonoDataRejectsInvalidCollectionWithoutReplacingData(int capacity, int firstKey, int secondKey)
    {
        var writer = TinyhandWriter.CreateFromBytePool();
        writer.WriteArrayHeader(2);
        writer.Write(capacity);
        writer.WriteArrayHeader(2);
        writer.WriteArrayHeader(2);
        writer.Write(firstKey);
        writer.Write("first");
        writer.WriteArrayHeader(2);
        writer.Write(secondKey);
        writer.Write("second");
        var bytes = writer.FlushAndGetArray();
        writer.Dispose();
        var data = new MonoData<int, string>(3);
        data.Set(9, "retained");

        TinyhandSerializer.DeserializeObject(bytes, ref data);

        Assert.NotNull(data);
        Assert.Equal(3, data.Capacity);
        Assert.Equal(1, data.Count);
        Assert.True(data.TryGet(9, out var retained));
        Assert.Equal("retained", retained);
    }

    [Fact]
    public void ZeroCapacityDoesNotRetainEntries()
    {
        var data = new MonoData<int, string>();
        data.Set(1, "ignored");
        Assert.Equal(0, data.Count);
        Assert.False(data.TryGet(1, out _));

        data.SetCapacity(1);
        data.Set(1, "retained");
        data.SetCapacity(0);
        Assert.Equal(0, data.Count);
    }

    [Fact]
    public void StorageComparerTreatsTwoNullConfigurationsAsEqual()
    {
        var comparer = StorageConfiguration.MainDirectoryComparer.Instance;
        var configuration = new SimpleStorageConfiguration(new LocalDirectoryConfiguration("primary"));
        var other = new SimpleStorageConfiguration(new LocalDirectoryConfiguration("primary"), new LocalDirectoryConfiguration("backup"));

        Assert.True(comparer.Equals(null, null));
        Assert.False(comparer.Equals(configuration, null));
        Assert.False(comparer.Equals(null, configuration));
        Assert.True(comparer.Equals(configuration, other));
        Assert.Equal(comparer.GetHashCode(configuration), comparer.GetHashCode(other));
    }

    [Fact]
    public async Task DisposedMemoryStreamCannotReadRetainedMemory()
    {
        var stream = new ReadOnlyMemoryStream(new byte[] { 1, 2, 3, });
        stream.Dispose();
        stream.Dispose();
        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.Throws<ObjectDisposedException>(() => stream.Length);
        Assert.Throws<ObjectDisposedException>(() => stream.Position);
        Assert.Throws<ObjectDisposedException>(() => stream.Position = 0);
        Assert.Throws<ObjectDisposedException>(() => stream.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[1]));
        Assert.Throws<ObjectDisposedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<ObjectDisposedException>(() => stream.Flush());
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            _ = await stream.ReadAsync(new byte[1], TestContext.Current.CancellationToken);
        });
    }
}
