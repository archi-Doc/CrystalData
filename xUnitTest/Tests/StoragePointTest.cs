// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData;
using Tinyhand;
using ValueLink;
using Xunit;

namespace xUnitTest.CrystalDataTest;

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.RepeatableRead)]
public partial record StoragePointClass : IEquatableObject<StoragePointClass>, IEquatable<StoragePointClass>
{
    public StoragePointClass(int id, string name, int number)
    {
        this.id = id;
        this.name = name;
        this.IntStorage.Set(2);
    }

    [Key(0, AddProperty = "Id", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    private int id;

    [Key(1, AddProperty = "Name")]
    [Link(Type = ChainType.Ordered)]
    private string name = string.Empty;

    [Key(2)]
    public StoragePoint<int> IntStorage { get; set; } = new();

    bool IEquatableObject<StoragePointClass>.ObjectEquals(StoragePointClass other)
        => ((IEquatable<StoragePointClass>)this).Equals(other);

    bool IEquatable<StoragePointClass>.Equals(StoragePointClass? other)
    {
        if (other is null)
        {
            return false;
        }

        return this.id == other.id &&
            this.name == other.name;
    }
}

public class StoragePointTest
{
    [Fact]
    public async Task Test0()
    {
        var tc = new StoragePointClass(1, "test", 22);
        var bin = TinyhandSerializer.Serialize(tc);
        var tc2 = TinyhandSerializer.Deserialize<StoragePointClass>(bin);

        bin = TinyhandSerializer.Serialize(tc, TinyhandSerializerOptions.Special);
        tc2 = TinyhandSerializer.Deserialize<StoragePointClass>(bin, TinyhandSerializerOptions.Special);
    }
}
