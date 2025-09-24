// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Diagnostics;
using System.Linq;
using Arc.Crypto;
using CrystalData;
using Tinyhand;
using ValueLink;
using Xunit;

namespace xUnitTest.CrystalDataTest;

#pragma warning disable SA1401 // Fields should be private
#pragma warning disable CS8602 // Dereference of a possibly null reference.

[TinyhandObject(Structual = true)]
public partial class SptClass2
{
    public SptClass2()
    {
    }

    [Key(0)]
    public partial int Id { get; private set; }

    [Key(1)]
    public partial int Count { get; set; }

    [Key(2)]
    public partial int Hash { get; set; }

    public void TryInitialize(int id)
    {
        if (id != 0)
        {
            this.Id = id;
            this.Hash = this.GetHashCode();
        }
    }

    public bool Validate()
    {
        return this.GetHashCode() == this.Hash;
    }

    public override int GetHashCode()
        => HashCode.Combine(this.Id, this.Count);

    public override string ToString()
    {
        return $"Id={this.Id}, Count={this.Count}, Hash={this.Hash}";
    }
}

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.ReadCommitted)]
public partial class SptPoint2 : StoragePoint<SptClass2>
{
    [Key(1)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    public int Id { get; set; }

    public void TryInitialize(int id)
    {
        using (var dataScope = this.TryLock(AcquisitionMode.GetOrCreate).Result)
        {
            if (dataScope.IsValid)
            {
                dataScope.Data.TryInitialize(id);
            }
            else
            {
                throw new Exception();
            }
        }
    }

    public override string ToString()
    {
        if (this.TryGet().Result is { } obj)
        {
            return obj.ToString() ?? string.Empty;
        }
        else
        {
            return string.Empty;
        }
    }
}

public class StoragePointTest4
{
    public const int MaxId = 100;
    public const int Concurrency = 1; // 100
    public const int Repetition = 1; // 100

    private Random random = new Random(11);
    private int totalCount = 0;
    private SptPoint2.GoshujinClass? g;

    [Fact]
    public async Task Test1()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<StoragePoint<SptPoint2.GoshujinClass>>(true);
        this.g = await crystal.Data.PinData();

        // await this.Setup();
        await this.Run();
        await this.Validate();

        await crystal.CrystalControl.StoreAndRelease(); // await crystal.Store(StoreMode.ForceRelease); await crystal.CrystalControl.StoreJournal();

        await this.Run();
        await this.Validate();

        await crystal.CrystalControl.StoreAndRelease(); // await crystal.Store(StoreMode.ForceRelease); await crystal.CrystalControl.StoreJournal();
        (await crystal.CrystalControl.TestJournalAll()).IsTrue();

        await TestHelper.StoreAndReleaseAndDelete(crystal);
    }

    private int GetRandomId()
        => this.random.Next(1, MaxId + 1);

    private async Task Increment(int id)
    {
        using (var dataScope = await this.g.TryLock(id, AcquisitionMode.GetOrCreate))
        {
            if (dataScope.IsValid)
            {
                ((IStructualObject)dataScope.Data).TryGetJournalWriter(out var writer);
                dataScope.Data.TryInitialize(id);
                dataScope.Data.Count++;
                dataScope.Data.Hash = dataScope.Data.GetHashCode();
            }
            else
            {
                throw new Exception();
            }
        }
    }

    private async Task StoreAndRelease(int id)
    {
        if (this.g.Find(id) is { } storagePoint)
        {
            await storagePoint.StoreData(StoreMode.TryRelease);
        }
    }

    private async Task<bool> Decrement(int id)
    {
        using (var dataScope = this.g.TryLock(id, AcquisitionMode.Get).Result)
        {
            if (dataScope.IsValid)
            {
                var data = dataScope.Data;
                if (data.Count > 0)
                {
                    dataScope.Data.Count--;
                    dataScope.Data.Hash = dataScope.Data.GetHashCode();
                }

                if (data.Count <= 0)
                {
                    await this.g.Delete(id);
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return false;
            }
        }
    }

    private Task Run()
    {
        var tasks = Enumerable.Range(1, Concurrency).Select(async x =>
        {
            for (int i = 0; i < Repetition; ++i)
            {
                var id = this.GetRandomId();
                await this.Increment(id);
                id = this.GetRandomId();
                await this.Increment(id);
                id = this.GetRandomId();
                // await this.Decrement(id);
                id = this.GetRandomId();
                // await this.StoreAndRelease(id);

                Interlocked.Increment(ref this.totalCount);
                Interlocked.Increment(ref this.totalCount);
            }
        });

        return Task.WhenAll(tasks);
    }

    private async Task Setup()
    {
        for (var i = 1; i <= MaxId; i++)
        {
            using (var dataScope = this.g.TryLock(i, AcquisitionMode.GetOrCreate).Result)
            {
                if (dataScope.IsValid)
                {
                    dataScope.Data.TryInitialize(i);
                }
                else
                {
                    throw new Exception();
                }
            }
        }
    }

    private async Task Validate()
    {
        var sum = 0;
        for (var i = 1; i <= MaxId; i++)
        {
            var c = await this.g.TryGet(i);
            if (c is not null)
            {
                sum += c.Count;
                c.Validate().IsTrue();
            }
        }

        sum.Is(this.totalCount);
    }
}
