// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData;
using Tinyhand;
using ValueLink;
using Xunit;

namespace xUnitTest.CrystalDataTest;

#pragma warning disable SA1401 // Fields should be private
#pragma warning disable CS8602 // Dereference of a possibly null reference.

[TinyhandObject(Structual = true)]
public partial class SptClass
{
    public SptClass()
    {
    }

    [Key(0)]
    public int Id { get; private set; }

    [Key(1)]
    public string Name = string.Empty;

    [Key(2)]
    public SptPoint.GoshujinClass SptGoshujin = new();

    [Key(3)]
    public StoragePoint<SptPoint.GoshujinClass> SptStorage { get; set; } = new();

    public void TryInitialize(int id, string name)
    {
        if (this.Id == 0)
        {
            this.Id = id;
            this.Name = name;
        }
    }

    public void CheckIdAndName(int id, string name)
    {
        this.Id.Is(id);
        this.Name.Is(name);
    }

    public async Task<SptClass> Add(int id, string name)
    {
        using (var dataScope = await this.SptGoshujin.TryLock(id, AcquisitionMode.GetOrCreate))
        {
            if (dataScope.IsValid)
            {
                dataScope.Data.TryInitialize(id, name);
                return dataScope.Data;
            }
            else
            {
                throw new Exception();
                // return default;
            }
        }
    }

    public async Task<SptClass> Add2(int id, string name)
    {
        using (var dataScope = await this.SptStorage.TryLock(id, AcquisitionMode.GetOrCreate))
        {
            if (dataScope.IsValid)
            {
                dataScope.Data.TryInitialize(id, name);
                return dataScope.Data;
            }
            else
            {
                throw new Exception();
                // return default;
            }
        }
    }
}

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.ReadCommitted)]
public partial class SptPoint : StoragePoint<SptClass>
{
    [Key(1)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    public int Id { get; set; }
}

public class StoragePointTest3
{
    [Fact]
    public async Task Test1()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<SptClass>(true);
        var c1 = crystal.Data;

        await this.Setup(c1);
        await this.Check(c1);

        var storage = c1.SptStorage;

        await crystal.Store(StoreMode.ForceRelease);
        await crystal.Crystalizer.StoreJournal();

        var v = await storage.TryGet(1);
        v = await storage.TryGet(20);

        await TestHelper.StoreAndReleaseAndDelete(crystal);
    }

    private async Task Setup(SptClass c1)
    {
        c1.TryInitialize(1, "Root");
        var c2 = await c1.Add(2, "Nu");
        var s2 = c1.SptGoshujin.Find(2);

        var c20 = await c1.Add2(20, "Po");
        c20.TryInitialize(20, "Po");

        c2 = await s2.TryGet();
        c2.IsNotNull();
        var r = await c1.SptGoshujin.Delete(2); // Remove from goshujin.
        c2 = await s2.TryGet();
        c2.IsNull();
        c2 = await c1.Add(2, "Nu");
        s2 = c1.SptGoshujin.Find(2);
        s2.IsNotNull();
        await s2.StoreData(StoreMode.ForceRelease);
        await s2.DeleteData(); // Remove from storage point.
        c2 = await s2.TryGet();
        c2.IsNull();

        c2 = await c1.Add(2, "Nu");
    }

    private async Task Check(SptClass c1)
    {
        c1.CheckIdAndName(1, "Root");
        c1.SptGoshujin.Count.Is(1);
        var array = c1.SptGoshujin.GetArray();
        array.Length.Is(1);
        (await array[0].TryGet()).CheckIdAndName(2, "Nu");
    }
}
