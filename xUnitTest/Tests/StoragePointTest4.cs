// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData;
using Tinyhand;
using ValueLink;
using Xunit;

namespace xUnitTest.CrystalDataTest;

#pragma warning disable SA1401 // Fields should be private
#pragma warning disable CS8602 // Dereference of a possibly null reference.

[TinyhandObject(Structural = true)]
public partial class SptClass2 : IEquatableObject
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

    public bool ObjectEquals(object? otherClass)
    {
        if (otherClass is not SptClass2 other)
        {
            return false;
        }

        return this.Id == other.Id && this.Count == other.Count && this.Hash == other.Hash;
    }
}

[TinyhandObject(Structural = true)]
[ValueLinkObject(Isolation = IsolationLevel.ReadCommitted)]
public partial class SptPoint2 : StoragePoint<SptClass2>, IEquatableObject
{
    public class EqualityComparer : IEqualityComparer<SptClass2>
    {
        public static readonly EqualityComparer Default = new();

        public bool Equals(SptClass2? x, SptClass2? y)
        {
            if (x is null)
            {
                return y is null;
            }

            return x.ObjectEquals(y);
        }

        public int GetHashCode(SptClass2 obj)
            => obj.GetHashCode();
    }

    public partial class GoshujinClass : IEquatableObject
    {
        public bool ObjectEquals(object? otherObject)
        {
            if (otherObject is not GoshujinClass other)
            {
                return false;
            }

            if (this.Count != other.Count)
            {
                return false;
            }

            using (this.LockObject.EnterScope())
            using (other.LockObject.EnterScope())
            {// might be dead-lock.
                foreach (var x in this.IdChain)
                {
                    var y = other.IdChain.FindFirst(x.Id);
                    if (y is null)
                    {
                        return false;
                    }

                    if (!y.ObjectEquals(x))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

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

    public bool ObjectEquals(object? otherObject)
    {
        if (otherObject is not SptPoint2 other)
        {
            return false;
        }

        if (this.PointId != other.PointId)
        {
            return false;
        }

        if (this.Id != other.Id)
        {
            return false;
        }

        var obj1 = this.TryGet().ConfigureAwait(false).GetAwaiter().GetResult();
        var obj2 = other.TryGet().ConfigureAwait(false).GetAwaiter().GetResult();
        if (obj1 is null)
        {
            return obj2 is null;
        }

        return obj1.ObjectEquals(obj2);
    }
}

public class StoragePointTest4
{
    public const int MaxId = 100;
    public const int Concurrency = 10; // 100
    public const int Repetition = 1000;

    private Random random = new Random(11);
    private int totalCount = 0;
    private SptPoint2.GoshujinClass? g;

    [Fact]
    public async Task Test1()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<StoragePoint<SptPoint2.GoshujinClass>>(true);
        this.g = await crystal.Data.PinData();

        // await this.Setup();
        await this.Test();
        await this.Run();
        await this.Validate();

        await crystal.CrystalControl.StoreAndRelease(TestContext.Current.CancellationToken); // await crystal.Store(StoreMode.ForceRelease); await crystal.CrystalControl.StoreJournal();

        await this.Run();
        await this.Validate();

        await crystal.CrystalControl.StoreAndRelease(TestContext.Current.CancellationToken); // await crystal.Store(StoreMode.ForceRelease); await crystal.CrystalControl.StoreJournal();
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
                dataScope.Data.TryInitialize(id);
                dataScope.Data.Count++;
                dataScope.Data.Hash = dataScope.Data.GetHashCode();

                Interlocked.Increment(ref this.totalCount);
            }
        }
    }

    private async Task Decrement(int id)
    {
        // var deleted = false;
        using (var dataScope = this.g.TryLock(id, AcquisitionMode.Get).Result)
        {
            if (dataScope.IsValid)
            {
                var data = dataScope.Data;
                if (data.Count > 0)
                {
                    dataScope.Data.Count--;
                    dataScope.Data.Hash = dataScope.Data.GetHashCode();

                    Interlocked.Decrement(ref this.totalCount);
                }

                if (data.Count <= 0)
                {
                    // deleted = true;
                    await dataScope.UnlockAndDelete();
                }
                else
                {
                }
            }
        }

        /*if (deleted)
        {// Deletion cannot be confirmed, as Increment may be called between deletion and confirmation.
            await Task.Delay(1);
            var d = await this.g.TryGet(id);
            d.IsNull();
            this.g.IdChain.FindFirst(id).IsNull();
        }*/
    }

    private async Task Test()
    {
        await this.Increment(1);
        await this.Decrement(1);
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
                await this.Decrement(id);
                id = this.GetRandomId();
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
