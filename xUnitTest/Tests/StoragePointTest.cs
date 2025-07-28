// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData;
using Tinyhand;
using ValueLink;
using Xunit;

namespace xUnitTest.CrystalDataTest;

[TinyhandObject(Structual = true)]
public partial record NoStoragePointClass : IEquatableObject<NoStoragePointClass>, IEquatable<NoStoragePointClass>
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

    bool IEquatableObject<NoStoragePointClass>.ObjectEquals(NoStoragePointClass other)
        => ((IEquatable<NoStoragePointClass>)this).Equals(other);

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
public sealed partial record StoragePointClass : IEquatableObject<StoragePointClass>, IEquatable<StoragePointClass>
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

    // [Key(3)]
    // public StoragePointStruct<string> StringStorage2 { get; set; }

    bool IEquatableObject<StoragePointClass>.ObjectEquals(StoragePointClass other)
        => ((IEquatable<StoragePointClass>)this).Equals(other);

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
    /*[Fact]
    public async Task Test1()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<StoragePointClass>(true);

        var g = crystal.Data;
        await crystal.Save(UnloadMode.ForceUnload);
        await crystal.Crystalizer.SaveJournal();

        await TestHelper.UnloadAndDeleteAll(crystal);
    }*/

    [Fact]
    public async Task Test0()
    {
        var tc = new StoragePointClass(1, "test", "22");
        var bin = TinyhandSerializer.Serialize(tc);
        var tc2 = TinyhandSerializer.Deserialize<StoragePointClass>(bin);

        bin = TinyhandSerializer.Serialize(tc, TinyhandSerializerOptions.Special);
        tc2 = TinyhandSerializer.Deserialize<StoragePointClass>(bin, TinyhandSerializerOptions.Special);

        tc.StringStorage.Configure(true);
        bin = TinyhandSerializer.Serialize(tc);
        tc2 = TinyhandSerializer.Deserialize<StoragePointClass>(bin);
        tc.Equals(tc2).IsTrue();

        var td = TinyhandSerializer.Deserialize<NoStoragePointClass>(bin);
        tc.Equals(td).IsTrue();
    }
}
