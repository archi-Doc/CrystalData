// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData;
using Tinyhand;
using ValueLink;
using Xunit;

namespace xUnitTest.CrystalDataTest;

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.Serializable)]
internal partial record SerializableData : IEquatableObject<SerializableData>
{
    public SerializableData()
    {
    }

    public SerializableData(int id, string name, double age)
    {
        this.id = id;
        this.name = name;
        this.age = age;
    }

    [Key(0, AddProperty = "Id")]
    [Link(Primary = true, Unique = true, Type = ChainType.Ordered)]
    private int id;

    [Key(1, AddProperty = "Name")]
    private string name = string.Empty;

    [Key(2, AddProperty = "Age")]
    private double age;

    public override string ToString()
        => $"{this.id} {this.name} ({this.age.ToString()})";

    public bool ObjectEquals(SerializableData other)
        => this.id == other.id && this.name == other.name && this.age == other.age;
}

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.RepeatableRead)]
internal partial record RepeatableData : IEquatableObject<RepeatableData>
{
    public RepeatableData()
    {
    }

    public RepeatableData(int id, string name, double age)
    {
        this.id = id;
        this.name = name;
        this.age = age;
    }

    [Key(0, AddProperty = "Id")]
    [Link(Primary = true, Unique = true, Type = ChainType.Ordered)]
    private int id;

    [Key(1, AddProperty = "Name")]
    private string name = string.Empty;

    [Key(2, AddProperty = "Age")]
    private double age;

    public override string ToString()
        => $"{this.id} {this.name} ({this.age.ToString()})";

    public bool ObjectEquals(RepeatableData other)
        => this.id == other.id && this.name == other.name && this.age == other.age;
}

public class JournalTest
{
    [Fact]
    public async Task TestSerializable()
    {
        var c = await TestHelper.CreateAndStartCrystal<SerializableData.GoshujinClass>();
        var g1 = c.Data;
        using (g1.LockObject.EnterScope())
        {
        }

        await c.Store(StoreMode.ForceRelease);
        await c.Crystalizer.StoreJournal();

        // g2: empty
        await c.PrepareAndLoad(false);
        var g2 = c.Data;
        g2.GoshujinEquals(g1).IsTrue();

        using (g2.LockObject.EnterScope())
        {
            g2.Count.Is(0);

            g2.Add(new(0, "Zero", 0));
        }

        await c.Store(StoreMode.ForceRelease);
        await c.Crystalizer.StoreJournal();

        // g3: Zero
        await c.PrepareAndLoad(false);
        var g3 = c.Data;
        g3.GoshujinEquals(g2).IsTrue();

        using (g3.LockObject.EnterScope())
        {
            g3.Count.Is(1);
            var d = g3.IdChain.FindFirst(0)!;
            d.IsNotNull();
            d.Id.Is(0);
            d.Name.Is("Zero");
            d.Age.Is(0d);
        }

        await c.Store(StoreMode.ForceRelease);
        await c.Crystalizer.StoreJournal();
        var result = await c.Crystalizer.TestJournalAll();
        result.IsTrue();

        // g4: 1, 2, 3, 4
        await c.PrepareAndLoad(false);
        var g4 = c.Data;
        g4.GoshujinEquals(g3).IsTrue();
        using (g4.LockObject.EnterScope())
        {
            g4.Add(new(1, "1", 1d));
            g4.Add(new(4, "4", 4d));
            g4.Add(new(3, "3", 3d));
            g4.Add(new(2, "2", 2d));

            var d = g4.IdChain.FindFirst(3)!;
            d.Goshujin = null;

            d = g4.IdChain.FindFirst(0)!;
            d.Id = 100;
        }

        await c.Store(StoreMode.ForceRelease);
        await c.Crystalizer.StoreJournal();
        result = await c.Crystalizer.TestJournalAll();
        result.IsTrue();

        // g5
        await c.PrepareAndLoad(false);
        var g5 = c.Data;
        g5.GoshujinEquals(g4).IsTrue();
        using (g5.LockObject.EnterScope())
        {
            var d = g5.IdChain.FindFirst(1)!;
            d.Name = "One";

            g5.IdChain.FindFirst(0).IsNull();
            d = g5.IdChain.FindFirst(100)!;
            d.Name = "100";
        }

        await c.Store(StoreMode.ForceRelease);
        await c.Crystalizer.StoreJournal();
        result = await c.Crystalizer.TestJournalAll();
        result.IsTrue();

        await TestHelper.StoreAndReleaseAndDelete(c);
    }

    [Fact]
    public async Task TestRepeatable()
    {
        var c = await TestHelper.CreateAndStartCrystal<RepeatableData.GoshujinClass>();
        var g1 = c.Data;
        using (g1.LockObject.EnterScope())
        {
        }

        await c.Store(StoreMode.ForceRelease);
        await c.Crystalizer.StoreJournal();

        // g2: empty
        await c.PrepareAndLoad(false);
        var g2 = c.Data;
        g2.GoshujinEquals(g1).IsTrue();

        g2.Count.Is(0);
        g2.Add(new(0, "Zero", 0));

        await c.Store(StoreMode.ForceRelease);
        await c.Crystalizer.StoreJournal();

        // g3: Zero
        await c.PrepareAndLoad(false);
        var g3 = c.Data;
        g3.GoshujinEquals(g2).IsTrue();
        {
            g3.Count.Is(1);
            var d = g3.TryGet(0)!;
            d.IsNotNull();
            d.Id.Is(0);
            d.Name.Is("Zero");
            d.Age.Is(0d);
        }

        await c.Store(StoreMode.ForceRelease);
        await c.Crystalizer.StoreJournal();
        var result = await c.Crystalizer.TestJournalAll();
        result.IsTrue();

        // g4: 1, 2, 3, 4
        await c.PrepareAndLoad(false);
        var g4 = c.Data;
        g4.GoshujinEquals(g3).IsTrue();
        {
            g4.Add(new(1, "1", 1d));
            g4.Add(new(4, "4", 4d));
            g4.Add(new(3, "3", 3d));
            g4.Add(new(2, "2", 2d));

            using (var w = g4.TryLock(3)!)
            {
                w.Goshujin = null;
                w.Commit();
            }

            using (var w = g4.TryLock(0)!)
            {
                w.Id = 100;
                w.Commit();
            }
        }

        await c.Store(StoreMode.ForceRelease);
        await c.Crystalizer.StoreJournal();
        result = await c.Crystalizer.TestJournalAll();
        result.IsTrue();

        // g5
        await c.PrepareAndLoad(false);
        var g5 = c.Data;
        g5.GoshujinEquals(g4).IsTrue();
        {
            using (var w = g5.TryLock(1)!)
            {
                w.Name = "One";
                w.Commit();
            }

            using (var w = g5.TryLock(100)!)
            {
                w.Name = "100";
                w.Commit();
            }
        }

        await c.Store(StoreMode.ForceRelease);
        await c.Crystalizer.StoreJournal();
        result = await c.Crystalizer.TestJournalAll();
        result.IsTrue();

        await TestHelper.StoreAndReleaseAndDelete(c);
    }
}
