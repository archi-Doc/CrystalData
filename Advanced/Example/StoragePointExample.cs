// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace Advanced.Example;

public class SptRoot
{
    public SptClass.GoshujinClass Data1 { get; set; } = new();

    public StoragePoint<SptClass.GoshujinClass> Data2 { get; set; } = new();

    public SptPoint.GoshujinClass Data3 { get; set; } = new();

    public StoragePoint<SptPoint.GoshujinClass> Data4 { get; set; } = new();
}

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.Serializable)]
public partial class SptClass
{
    [Key(0)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    public int Id { get; private set; }

    [Key(1)]
    public string Name { get; private set; } = string.Empty;

    public SptClass()
    {
    }

    public void TryInitialize(int id, string name)
    {
        if (this.Id == 0)
        {
            this.Id = id;
            this.Name = name;
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
