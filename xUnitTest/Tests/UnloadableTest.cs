// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Arc.Crypto;
using CrystalData;
using Tinyhand;
using ValueLink;
using Xunit;

namespace xUnitTest.CrystalDataTest;

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.RepeatableRead)]
public partial record UnloadableClass : IEquatableObject<UnloadableClass>, IEquatable<UnloadableClass>
{
    public UnloadableClass()
    {
    }

    [Key(0, AddProperty = "Id", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    private int id;

    [Key(1, AddProperty = "Name")]
    [Link(Type = ChainType.Ordered)]
    private string name = string.Empty;

    [Key(2, AddProperty = "Child", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    private StorageData<UnloadableClass> child = new();

    [Key(3, AddProperty = "Children", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    private StorageData<UnloadableClass.GoshujinClass> children = new();

    [Key(4, AddProperty = "ByteArray", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    private StorageData<byte[]> byteArray = new();

    bool IEquatableObject<UnloadableClass>.ObjectEquals(UnloadableClass other)
        => ((IEquatable<UnloadableClass>)this).Equals(other);

    bool IEquatable<UnloadableClass>.Equals(UnloadableClass? other)
    {
        if (other is null)
        {
            return false;
        }

        return this.id == other.id &&
            this.name == other.name &&
            this.child.Get().Result.Equals(other.child.Get().Result) &&
            this.children.Get().Result.GoshujinEquals(other.children.Get().Result) &&
            this.byteArray.Get().Result.SequenceEqual(other.byteArray.Get().Result);
    }
}

public class UnloadableTest
{
    [Fact]
    public async Task Test1()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<UnloadableClass.GoshujinClass>(true);

        var g = crystal.Data;
        await crystal.Save(UnloadMode.ForceUnload);
        await crystal.Crystalizer.SaveJournal();

        // g2: empty
        await crystal.PrepareAndLoad(false);
        var g2 = crystal.Data;
        g2.GoshujinEquals(g).IsTrue();

        // Save & Test journal
        await crystal.Save(UnloadMode.ForceUnload);
        await crystal.Crystalizer.SaveJournal();
        var result = await crystal.Crystalizer.TestJournalAll();
        result.IsTrue();

        // g3: +1 -1
        await crystal.PrepareAndLoad(false);
        var g3 = crystal.Data;

        using (var w = g3.TryLock(1, TryLockMode.GetOrCreate)!)
        {
            w.Name = "One";
            w.Commit();
        }

        var r = g3.TryGet(1)!;
        var children = await r.Children.Get();
        using (var w2 = children.TryLock(2, TryLockMode.GetOrCreate)!)
        {
            w2.Commit();
        }

        using (var w2 = children.TryLock(2, TryLockMode.GetOrCreate)!)
        {
            w2.RemoveAndErase();
            w2.Commit();
        }

        await r.Children.StoreData(UnloadMode.ForceUnload);

        r.Children.Erase();

        // Save & Test journal
        await crystal.Save(UnloadMode.ForceUnload);
        await crystal.Crystalizer.SaveJournal();
        result = await crystal.Crystalizer.TestJournalAll();
        result.IsTrue();

        await TestHelper.UnloadAndDeleteAll(crystal);
    }
}
