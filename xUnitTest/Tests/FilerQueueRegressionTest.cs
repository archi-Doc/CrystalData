// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Collections.Concurrent;
using Arc.Collections;
using Arc.Threading;
using CrystalData;
using CrystalData.Filer;
using Xunit;

namespace xUnitTest.CrystalDataTest;

public class FilerQueueRegressionTest
{
    [Fact]
    public async Task MissingFileIsDistinguishedFromReadErrors()
    {
        var directory = Directory.CreateTempSubdirectory("CrystalData-FilerQueue-").FullName;
        using var filer = new LocalFiler(new ExecutionRoot());
        try
        {
            IFiler raw = filer;
            var missing = await raw.ReadAsync(Path.Combine(directory, "missing"), 0, -1);
            Assert.Equal(CrystalResult.NotFound, missing.Result);
            missing.Return();
            missing = await raw.ReadAsync(Path.Combine(directory, "missing", "file"), 0, -1);
            Assert.Equal(CrystalResult.NotFound, missing.Result);
            missing.Return();
            var unreadable = await raw.ReadAsync(directory, 0, -1);
            Assert.Equal(CrystalResult.FileOperationError, unreadable.Result);
            unreadable.Return();
        }
        finally
        {
            await ((IFiler)filer).FlushAsync(true);
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task QueuedOperationTimeoutIncludesWaitingForTheSamePath()
    {
        var directory = Directory.CreateTempSubdirectory("CrystalData-FilerQueue-").FullName;
        using var filer = new GatedFiler(new ExecutionRoot());
        try
        {
            IFiler raw = filer;
            var path = Path.Combine(directory, "data");
            var first = raw.WriteAsync(path, 0, BytePool.RentedReadOnlyMemory.CreateFrom([1]));
            await filer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var second = raw.WriteAsync(path, 0, BytePool.RentedReadOnlyMemory.CreateFrom([2]), TimeSpan.FromMilliseconds(20));
            Assert.Equal(CrystalResult.Aborted, await second.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            filer.Release.TrySetResult();
            Assert.Equal(CrystalResult.Success, await first);
            await raw.FlushAsync(false);
            Assert.Equal(new byte[] { 2 }, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        }
        finally
        {
            filer.Release.TrySetResult();
            await ((IFiler)filer).FlushAsync(true);
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task FlushDrainsFireAndForgetOperationsInSubmissionOrder()
    {
        var directory = Directory.CreateTempSubdirectory("CrystalData-FilerQueue-").FullName;
        using var filer = new GatedFiler(new ExecutionRoot());
        try
        {
            IFiler raw = filer;
            var path = Path.Combine(directory, "data");
            for (var i = 0; i < 64; i++)
            {
                Assert.Equal(CrystalResult.Started, raw.WriteAndForget(path, 0, BytePool.RentedReadOnlyMemory.CreateFrom([(byte)i])));
            }

            await filer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var flush = raw.FlushAsync(true);
            Assert.False(flush.IsCompleted);
            filer.Release.TrySetResult();
            await flush.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(Enumerable.Range(0, 64).Select(x => (byte)x), filer.WrittenValues);
            Assert.Equal(new byte[] { 63 }, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        }
        finally
        {
            filer.Release.TrySetResult();
            await ((IFiler)filer).FlushAsync(true);
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task EquivalentLocalPathsShareTheSameQueue()
    {
        var directory = Directory.CreateTempSubdirectory("CrystalData-FilerQueue-").FullName;
        using var filer = new GatedFiler(new ExecutionRoot());
        try
        {
            IFiler raw = filer;
            var path = Path.Combine(directory, "data");
            var first = raw.WriteAsync(path, 0, BytePool.RentedReadOnlyMemory.CreateFrom([1]));
            await filer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var alias = Path.Combine(directory, ".", "data");
            if (OperatingSystem.IsWindows())
            {
                alias = alias.ToUpperInvariant();
            }

            var second = raw.WriteAsync(alias, 0, BytePool.RentedReadOnlyMemory.CreateFrom([2]));
            Assert.False(second.IsCompleted);
            filer.Release.TrySetResult();
            Assert.All(await Task.WhenAll(first, second), result => Assert.Equal(CrystalResult.Success, result));
            Assert.Equal(new byte[] { 1, 2 }, filer.WrittenValues);
            Assert.Equal(new byte[] { 2 }, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        }
        finally
        {
            filer.Release.TrySetResult();
            await ((IFiler)filer).FlushAsync(true);
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task DisposedFilerReleasesWriteOwnershipWithoutProcessing()
    {
        using var filer = new LocalFiler(new ExecutionRoot());
        filer.Dispose();
        using var owner = BytePool.Default.Rent(1);
        Assert.Equal(CrystalResult.Aborted, await ((IFiler)filer).WriteAsync("unused", 0, owner.AsReadOnlyMemory(0, 1)));
        Assert.Equal(1, owner.ReferenceCount);
    }

    [Fact]
    public async Task CompletedJobsAreReturnedToThePool()
    {
        var directory = Directory.CreateTempSubdirectory("CrystalData-FilerQueue-").FullName;
        using var filer = new GatedFiler(new ExecutionRoot());
        try
        {
            filer.Release.TrySetResult();
            IFiler raw = filer;
            var path = Path.Combine(directory, "data");
            Assert.Equal(CrystalResult.Success, await raw.WriteAsync(path, 0, BytePool.RentedReadOnlyMemory.CreateFrom([1])));
            Assert.Equal(CrystalResult.Success, await raw.WriteAsync(path, 0, BytePool.RentedReadOnlyMemory.CreateFrom([2])));
            var jobs = filer.ProcessedJobs.ToArray();
            Assert.Same(jobs[0], jobs[1]);
        }
        finally
        {
            await ((IFiler)filer).FlushAsync(true);
            TestHelper.TryDeleteDirectory(directory);
        }
    }

    private sealed class GatedFiler : LocalFiler
    {
        private int started;

        public GatedFiler(ExecutionRoot root)
            : base(root)
        {
        }

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentQueue<byte> WrittenValues { get; } = new();

        public ConcurrentQueue<FilerWork> ProcessedJobs { get; } = new();

        protected override async Task ProcessJobAsync(FilerWork work, CancellationToken cancellationToken)
        {
            this.ProcessedJobs.Enqueue(work);
            if (Interlocked.Increment(ref this.started) == 1)
            {
                this.Entered.TrySetResult();
                await this.Release.Task.WaitAsync(cancellationToken);
            }

            if (work.Type == FilerWork.WorkType.Write)
            {
                this.WrittenValues.Enqueue(work.WriteData.Span[0]);
            }

            await base.ProcessJobAsync(work, cancellationToken);
        }
    }
}
