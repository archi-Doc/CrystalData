// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData;
using Tinyhand;
using ValueLink;
using Xunit;

namespace xUnitTest.CrystalDataTest;

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.RepeatableRead)]
public partial record StorageDataClass : IEquatableObject<StorageDataClass>, IEquatable<StorageDataClass>
{
    public StorageDataClass()
    {
    }

    [Key(0, AddProperty = "Id", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    private int id;

    [Key(1, AddProperty = "Name")]
    [Link(Type = ChainType.Ordered)]
    private string name = string.Empty;

    [Key(2, AddProperty = "Child", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    private StoragePoint<StorageDataClass> child = new();

    [Key(3, AddProperty = "Children", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    private StoragePoint<StorageDataClass.GoshujinClass> children = new();

    [Key(4, AddProperty = "ByteArray", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    private StoragePoint<byte[]> byteArray = new();

    bool IEquatableObject<StorageDataClass>.ObjectEquals(StorageDataClass other)
        => ((IEquatable<StorageDataClass>)this).Equals(other);

    bool IEquatable<StorageDataClass>.Equals(StorageDataClass? other)
    {
        if (other is null)
        {
            return false;
        }

        return this.id == other.id &&
            this.name == other.name &&
            this.child.DataEquals(other.child) &&
            this.children.DataEquals(other.children) &&
            this.byteArray.DataEquals(other.byteArray);
    }
}

public class StorageDataTest
{
    [Fact]
    public async Task Test1()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<StorageDataClass.GoshujinClass>(true);

        var g = crystal.Data;
        await crystal.Store(StoreMode.ForceRelease);
        await crystal.Crystalizer.StoreJournal();

        // g2: empty
        await crystal.PrepareAndLoad(false);
        var g2 = crystal.Data;
        g2.GoshujinEquals(g).IsTrue();

        // Save & Test journal
        await crystal.Store(StoreMode.ForceRelease);
        await crystal.Crystalizer.StoreJournal();
        var result = await crystal.Crystalizer.TestJournalAll();
        result.IsTrue();

        // g3: +1 -1
        await crystal.PrepareAndLoad(false);
        var g3 = crystal.Data;

        using (var w = g3.TryLock(1, LockMode.GetOrCreate)!)
        {
            w.Name = "One";
            w.Commit();
        }

        var r = g3.TryGet(1)!;
        using (var children = await r.Children.TryLock())
        {
            using (var w2 = children.Data!.TryLock(2, LockMode.GetOrCreate)!)
            {
                w2.Commit();
            }

            using (var w2 = children.Data!.TryLock(2, LockMode.GetOrCreate)!)
            {
                w2.DeleteAndErase();
                w2.Commit();
            }
        }

        await r.Children.StoreData(StoreMode.ForceRelease);

        r.Children.Delete();

        // Save & Test journal
        await crystal.Store(StoreMode.ForceRelease);
        await crystal.Crystalizer.StoreJournal();
        result = await crystal.Crystalizer.TestJournalAll();
        result.IsTrue();

        await TestHelper.UnloadAndDeleteAll(crystal);
    }
}
