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
    public StoragePoint<string> TextStorage = new();

    [Key(3)]
    public SptInt.GoshujinClass SptInt = new();

    [Key(4)]
    public StoragePoint<SptPoint.GoshujinClass> SptStorage { get; set; } = new();

    public void TryInitialize(int id, string name, string text)
    {
        if (this.Id == 0)
        {
            this.Id = id;
            this.Name = name;
            this.TextStorage.Set(text);
        }
    }

    public void CheckIdAndName(int id, string name, string text, IEnumerable<int> sptInt)
    {
        this.Id.Is(id);
        this.Name.Is(name);
        this.TextStorage.TryGet().Result.Is(text);
        this.SptInt.Equals(sptInt).IsTrue();
    }

    public async Task<SptClass> Add(int id, string name, string text)
    {
        using (var dataScope = await this.SptStorage.TryLock(id, AcquisitionMode.GetOrCreate))
        {
            if (dataScope.IsValid)
            {
                dataScope.Data.TryInitialize(id, name, text);
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
[ValueLinkObject(Isolation = IsolationLevel.None)]
public partial class SptInt
{
    [TinyhandObject(External = true)]
    public partial class GoshujinClass
    {
        public bool Equals(IEnumerable<int> span)
        {
            return this.ValueChain.Select(x => x.Value).SequenceEqual(span);
        }
    }

    [Key(0)]
    [Link(Primary = true, Unique = true, Type = ChainType.Ordered)]
    public int Value { get; set; }

    public SptInt()
    {
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
        c1.TryInitialize(1, "Root", "R");
        var c2 = await c1.Add(2, "Nu", "Po");
        var s2 = await c1.SptStorage.Find(2);

        var c20 = await c1.Add(20, "Nu", "Poo");

        c2 = await s2.TryGet();
        c2.IsNotNull();
        var r = await c1.SptStorage.Delete(2); // Perform deletion from the goshujin side.
        c2 = await s2.TryGet();
        c2.IsNull();
        c2 = await c1.Add(2, "Nu", "Po");
        s2 = await c1.SptStorage.Find(2);
        s2.IsNotNull();
        await s2.StoreData(StoreMode.ForceRelease);
        await s2.DeleteData(); // Perform deletion from the object side.
        c2 = await s2.TryGet();
        c2.IsNull();

        c2 = await c1.Add(2, "Nu", "Po");
    }

    private async Task Check(SptClass c1)
    {
        c1.CheckIdAndName(1, "Root", "R", []);
        var array = (await c1.SptStorage.TryGet()).GetArray();
        array.Length.Is(2);
        (await array[0].TryGet()).CheckIdAndName(2, "Nu", "Po", []);
        (await array[1].TryGet()).CheckIdAndName(20, "Nu", "Poo", []);
    }
}
