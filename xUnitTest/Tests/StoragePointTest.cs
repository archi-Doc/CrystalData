// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Arc.Threading;
using CrystalData;
using Tinyhand;
using ValueLink;
using Xunit;

namespace xUnitTest.CrystalDataTest;

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.RepeatableRead)]
public sealed partial record StoragePointClass1
{
    public StoragePointClass1(int id, string name)
    {
        this.Id = id;
        this.Name = name;
    }

    private readonly Lock lockObject = new();

    [Key(0)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    public int Id { get; set; }

    [Key(1)]
    public string Name { get; set; }

    [Key(2)]
    public StoragePointClass Class1 { get; set; } = new(1, "Class1", "Description of Class1");

    [Key(3)]
    public StoragePoint<StoragePointClass> Class2 { get; set; } = new();
}

[TinyhandObject(Structual = false)]
public partial record NoStoragePointClass : IEquatableObject, IEquatable<NoStoragePointClass>
{
    public NoStoragePointClass(int id, string name, string descrption)
    {
        this.id = id;
        this.name = name;
        this.Description = descrption;
    }

    [Key(0, AddProperty = "Id", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    private int id;

    [Key(1, AddProperty = "Name")]
    [Link(Type = ChainType.Ordered)]
    private string name = string.Empty;

    [Key(2)]
    public string Description { get; set; } = string.Empty;

    public bool ObjectEquals(object? otherObject)
        => this.Equals(otherObject as NoStoragePointClass);

    bool IEquatable<NoStoragePointClass>.Equals(NoStoragePointClass? other)
    {
        if (other is null)
        {
            return false;
        }

        return this.id == other.id &&
            this.name == other.name &&
            this.Description == other.Description;
    }
}

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.RepeatableRead)]
public sealed partial record StoragePointClass : IEquatableObject, IEquatable<StoragePointClass>
{
    public StoragePointClass(int id, string name, string descrption)
    {
        this.id = id;
        this.name = name;
        this.StringStorage.Set(descrption);
    }

    [Key(0, AddProperty = "Id", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    private int id;

    [Key(1, AddProperty = "Name")]
    [Link(Type = ChainType.Ordered)]
    private string name = string.Empty;

    [Key(2)]
    public StoragePoint<string> StringStorage { get; set; } = new();

    public bool ObjectEquals(object? other)
        => this.Equals(other as StoragePointClass);

    public bool Equals(StoragePointClass? other)
    {
        if (other is null)
        {
            return false;
        }

        return this.id == other.id &&
            this.name == other.name &&
            this.StringStorage.DataEquals(other.StringStorage);
    }

    public bool Equals(NoStoragePointClass? other)
    {
        if (other is null)
        {
            return false;
        }

        return this.id == other.Id &&
            this.name == other.Name &&
            this.StringStorage.TryGet().Result == other.Description;
    }

    public override int GetHashCode()
        => HashCode.Combine(this.id, this.name, this.StringStorage.TryGet().Result);
}

public class StoragePointTest
{
    [Fact]
    public async Task Test2()
    {
        var g = new StoragePointClass1.GoshujinClass();
        using (var writer = g.TryLock(1, AcquisitionMode.GetOrCreate))
        {
            if (writer is not null)
            {
                writer.Name = "Test";
                using var c2 = await writer.Class2.TryLock();
                writer.Commit();
            }
        }
    }

    [Fact]
    public async Task Test1()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<StoragePointClass>(true);

        var g = crystal.Data;
        var st = await g.StringStorage.TryGet();

        var t = await g.StringStorage.TryLock();
        if (t.Data is not null)
        {
            g.StringStorage.Unlock();
        }

        g.StringStorage.Set("Test String");
        // await g.StringStorage.DeleteData();

        await crystal.StoreData(StoreMode.ForceRelease);
        // await crystal.CrystalControl.StoreAndRelease();

        g.StringStorage.Set("Test String2");
        await crystal.CrystalControl.StoreAndRelease();

        st = await g.StringStorage.TryGet();
        st.Is("Test String2");

        await crystal.CrystalControl.StoreJournal();
        var jr = await crystal.CrystalControl.TestJournalAll();
        jr.IsTrue();

        await TestHelper.StoreAndReleaseAndDelete(crystal);
    }

    [Fact]
    public async Task Test0()
    {
        var tc = new StoragePointClass(1, "test", "22");
        var bin = TinyhandSerializer.Serialize(tc);
        var tc2 = TinyhandSerializer.Deserialize<StoragePointClass>(bin);

        bin = TinyhandSerializer.Serialize(tc, TinyhandSerializerOptions.Special);
        tc2 = TinyhandSerializer.Deserialize<StoragePointClass>(bin, TinyhandSerializerOptions.Special);

        bin = TinyhandSerializer.Serialize(tc);
        tc2 = TinyhandSerializer.Deserialize<StoragePointClass>(bin);
        tc.Equals(tc2).IsTrue();

        var td = TinyhandSerializer.Deserialize<NoStoragePointClass>(bin);
        tc.Equals(td).IsTrue();
    }
}
