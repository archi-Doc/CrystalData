﻿// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData;
using Tinyhand;
using ValueLink;
using Xunit;

namespace xUnitTest.CrystalDataTest;

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.RepeatableRead)]
public partial record CreditData
{
    public CreditData()
    {
    }

    [Link(Primary = true, Unique = true, Type = ChainType.Unordered)]
    [Key(0, AddProperty = "Credit", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    private int credit;

    [Key(3, AddProperty = "Borrowers", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    private StorageData<Borrower.GoshujinClass> borrowers = new();
}

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.RepeatableRead)]
public sealed partial record Borrower // : ITinyhandCustomJournal
{
    public Borrower()
    {
    }

    [Key(0)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    private long id;
}

public class CrystalTest
{
    [Fact]
    public async Task Test1()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<CreditData.GoshujinClass>(true);

        var g = crystal.Data;
        await crystal.Save(UnloadMode.ForceUnload);

        await crystal.PrepareAndLoad(false);
        g = crystal.Data;

        CreditData creditData;
        using (var w = g.TryLock(1, TryLockMode.GetOrCreate)!)
        {
            creditData = w.Commit()!;
        }

        var borrowers = await creditData.Borrowers.Get();
        using (var w2 = borrowers.TryLock(22, TryLockMode.Create)!)
        {
            w2.Commit();
        }

        await crystal.Save(UnloadMode.ForceUnload);
        await crystal.PrepareAndLoad(false);
        g = crystal.Data;

        var ww = g.TryGet(1);
        var ww2 = await ww!.Borrowers.Get();
        var ww3 = ww2.TryGet(22);
        ww3.IsNotNull();

        await TestHelper.UnloadAndDeleteAll(crystal);
    }

    [Fact]
    public async Task Test2()
    {
        var crystal = await TestHelper.CreateAndStartCrystal2<CreditData.GoshujinClass>();

        var g = crystal.Data;
        await crystal.Save(UnloadMode.ForceUnload);

        await crystal.PrepareAndLoad(false);
        g = crystal.Data;

        CreditData creditData;
        using (var w = g.TryLock(1, TryLockMode.GetOrCreate)!)
        {
            creditData = w.Commit()!;
        }

        var borrowers = await creditData.Borrowers.Get();
        using (var w2 = borrowers.TryLock(22, TryLockMode.Create)!)
        {
            w2.Commit();
        }

        await crystal.Save(UnloadMode.ForceUnload);
        await crystal.PrepareAndLoad(false);
        g = crystal.Data;

        var ww = g.TryGet(1);
        var ww2 = await ww!.Borrowers.Get();
        var ww3 = ww2.TryGet(22);
        ww3.IsNotNull();

        await TestHelper.UnloadAndDeleteAll(crystal);
    }
}
