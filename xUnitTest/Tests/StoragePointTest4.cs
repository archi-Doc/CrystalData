// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Linq;
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
    public int Id { get; private set; }

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
    public const int Max = 100;

    private int totalCount = 0;
    private SptPoint2.GoshujinClass? g;

    [Fact]
    public async Task Test1()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<StoragePoint<SptPoint2.GoshujinClass>>(true);
        this.g = await crystal.Data.PinData();

        // await this.Setup(c1);
        await this.Validate();

        await crystal.CrystalControl.StoreAndRelease(); // await crystal.Store(StoreMode.ForceRelease); await crystal.CrystalControl.StoreJournal();

        await this.Validate();

        await crystal.CrystalControl.StoreAndRelease(); // await crystal.Store(StoreMode.ForceRelease); await crystal.CrystalControl.StoreJournal();
        (await crystal.CrystalControl.TestJournalAll()).IsTrue();

        await TestHelper.StoreAndReleaseAndDelete(crystal);
    }

    private async Task Increment(SptPoint2 c1, int id)
    {
        using (var dataScope = c1.TryLock(AcquisitionMode.GetOrCreate).Result)
        {
            if (dataScope.IsValid)
            {
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

    private async Task<bool> Decrement(SptPoint2 c1, int id)
    {
        using (var dataScope = c1.TryLock(AcquisitionMode.Get).Result)
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
                    c1.Goshujin.Delete(id);
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

    private async Task Setup(SptPoint2 c1)
    {
        for (var i = 1; i <= Max; i++)
        {
            c1.TryInitialize(i);
        }
    }

    private async Task Validate()
    {
        for (var i = 1; i <= Max; i++)
        {
            var c = await this.g.TryGet(i);
            if (c is not null)
            {
                this.totalCount += c.Count;
                c.Validate().IsTrue();
            }
        }

        this.totalCount.Is(N);
    }
}
