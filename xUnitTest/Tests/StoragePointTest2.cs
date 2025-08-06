// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Arc.Threading;
using CrystalData;
using Tinyhand;
using ValueLink;
using Xunit;

namespace xUnitTest.CrystalDataTest;

[TinyhandObject(Structual = true, LockObject = "lockObject")]
public partial record SpRootClass
{
    public SpRootClass()
    {
    }

    private readonly Lock lockObject = new();

    [Key(0)]
    public string Name { get; set; } = string.Empty;

    [Key(1)]
    public StoragePoint<string> NameStorage { get; set; } = new();

    [Key(2)]
    public SpFirstClass FirstClass { get; set; } = new();

    [Key(3)]
    public StoragePoint<SpFirstClass> FirstClassStorage { get; set; } = new();
}

[TinyhandObject(Structual = true, LockObject = "LockObject")]
public partial record SpFirstClass
{
    [IgnoreMember]
    public SemaphoreLock LockObject { get; } = new();

    [Key(0)]
    public int Id { get; set; }
}

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.Serializable)]
public partial record SpSecondClass
{
    [Key(0)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    public int Id { get; set; }
}

public class StoragePointTest2
{
    [Fact]
    public async Task Test1()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<SpRootClass>(true);

        var root = crystal.Data;
        root.Name = "Test1";
        var name = await root.NameStorage.TryGet();
        name.IsNull();
        root.NameStorage.Set("Test2");
        (await root.NameStorage.TryGet()).Is("Test2");

        var firstClass = root.FirstClass;
        using (firstClass.LockObject.Lock())
        {
            firstClass.Id = 123;
        }

        firstClass = await root.FirstClassStorage.GetOrCreate();
        if (await root.FirstClassStorage.TryLock() is { } firstClass2)
        {
            using (firstClass2.LockObject.Lock())
            {
                firstClass2.Id = 456;
            }

            root.FirstClassStorage.Unlock();
        }

        await crystal.Store(StoreMode.Release);
        await crystal.Crystalizer.StoreJournal();
        await this.CheckData(crystal.Data);

        await TestHelper.UnloadAndDeleteAll(crystal);
    }

    private async Task CheckData(SpRootClass root)
    {
        root.Name.Is("Test1");
        (await root.NameStorage.TryGet()).Is("Test2");

        root.FirstClass.Id.Is(123);
        (await root.FirstClassStorage.TryGet())!.Id.Is(456);
    }
}
