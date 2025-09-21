// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Linq;
using CrystalData;
using Tinyhand;
using ValueLink;
using Xunit;

namespace xUnitTest.CrystalDataTest;

#pragma warning disable SA1401 // Fields should be private
#pragma warning disable CS8602 // Dereference of a possibly null reference.
public static class StoragePointTestHelper
{
    public static void Validate(this SptPoint? sptPoint, int id, string name, string text, IEnumerable<int> sptInt)
    {
        var sptClass = sptPoint.TryGet().Result;
        sptClass.IsNotNull();
        sptClass.Validate(id, name, text, sptInt);
    }

    public static SptPoint[] GetSptArray(this SptPoint? sptPoint)
    {
        return sptPoint.TryGet().Result.GetSptArray();
    }
}

[TinyhandObject(Structual = true)]
public partial class SptClass
{
    public SptClass()
    {
    }

    [Key(0)]
    public int Id { get; private set; }

    [Key(1)]
    public partial string Name { get; set; } = string.Empty;

    [Key(2)]
    public StoragePoint<string> TextStorage = new();

    [Key(3)]
    public SptInt.GoshujinClass SptInt = new();

    [Key(4)]
    public StoragePoint<SptPoint.GoshujinClass> SptStorage { get; set; } = new();

    public void TryInitialize(int id, string name, string text, IEnumerable<int> numbers)
    {
        if (this.Id == 0)
        {
            this.Id = id;
            this.Name = name;
            this.TextStorage.Set(text);
            foreach (var x in numbers)
            {
                this.SptInt.Add(new(x));
            }
        }
    }

    public void Validate(int id, string name, string text, IEnumerable<int> sptInt)
    {
        this.Id.Is(id);
        this.Name.Is(name);
        this.TextStorage.TryGet().Result.Is(text);
        this.SptInt.Equals(sptInt).IsTrue();
    }

    public void ValidateChildren(IEnumerable<int> numbers)
    {
        var data = this.SptStorage.TryGet().Result;
        data.IsNotNull();
        data.IdChain.Select(x => x.Id).SequenceEqual(numbers.OrderBy(x => x)).IsTrue();
    }

    public SptClass Add(int id, string name, string text, IEnumerable<int> numbers)
    {
        using (var dataScope = this.SptStorage.TryLock(id, AcquisitionMode.GetOrCreate).Result)
        {
            if (dataScope.IsValid)
            {
                dataScope.Data.TryInitialize(id, name, text, numbers);
                return dataScope.Data;
            }
            else
            {
                throw new Exception();
                // return default;
            }
        }
    }

    public SptPoint[] GetSptArray()
    {
        return this.SptStorage.TryGet().Result.GetArray();
    }

    public Dictionary<int, SptClass> ToDictionary()
    {
        var dic = new Dictionary<int, SptClass>();
        this.AddToDictionary(dic);
        return dic;
    }

    public Dictionary<int, SptPoint> ToPoints()
    {
        var dic = new Dictionary<int, SptPoint>();
        this.AddToDictionary(dic);
        return dic;
    }

    public override string ToString()
    {
        return $"{this.Id}: {this.Name} ({this.TextStorage.TryGet().Result}) [{string.Join(",", this.SptInt.NumberChain.Select(x => x.Number))}]";
    }

    private void AddToDictionary(Dictionary<int, SptClass> dic)
    {
        dic[this.Id] = this;
        var g = this.SptStorage.TryGet().Result;
        if (g is null)
        {
            return;
        }

        foreach (var x in g.IdChain)
        {
            var sptClass = x.TryGet().Result;
            sptClass.AddToDictionary(dic);
        }
    }

    private void AddToDictionary(Dictionary<int, SptPoint> dic)
    {
        var g = this.SptStorage.TryGet().Result;
        if (g is null)
        {
            return;
        }

        foreach (var x in g.IdChain)
        {
            dic[x.Id] = x;
            x.TryGet().Result.AddToDictionary(dic);
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
            return this.NumberChain.Select(x => x.Number).SequenceEqual(span);
        }
    }

    [Key(0)]
    [Link(Primary = true, Unique = true, Type = ChainType.Ordered)]
    public int Number { get; set; }

    public SptInt()
    {
    }

    public SptInt(int number)
    {
        this.Number = number;
    }
}

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.ReadCommitted)]
public partial class SptPoint : StoragePoint<SptClass>
{
    [Key(1)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    public int Id { get; set; }

    public void TryInitialize(int id, string name, string text, IEnumerable<int> numbers)
    {
        using (var dataScope = this.TryLock(AcquisitionMode.GetOrCreate).Result)
        {
            if (dataScope.IsValid)
            {
                dataScope.Data.TryInitialize(id, name, text, numbers);
            }
            else
            {
                throw new Exception();
            }
        }
    }

    public SptClass Add(int id, string name, string text, IEnumerable<int> numbers)
    {
        var data = this.TryGet().Result;
        using (var dataScope = data.SptStorage.TryLock(id, AcquisitionMode.GetOrCreate).Result)
        {
            if (dataScope.IsValid)
            {
                dataScope.Data.TryInitialize(id, name, text, numbers);
                return dataScope.Data;
            }
            else
            {
                throw new Exception();
            }
        }
    }

    public void ValidateDeleted()
    {
        this.IsDeleted.IsTrue();
        this.TryGet().Result.IsNull();
        this.TryLock().Result.Result.Is(DataScopeResult.Obsolete);
    }

    public Dictionary<int, SptPoint> ToDictionary()
    {
        var dic = new Dictionary<int, SptPoint>();
        this.AddToDictionary(dic);
        return dic;
    }

    public override string ToString()
    {
        if (this.TryGet().Result is { } obj)
        {
            return obj.ToString() ?? string.Empty;
        }
        else
        {
            return string.Empty;
        }
    }

    private void AddToDictionary(Dictionary<int, SptPoint> dic)
    {
        dic[this.Id] = this;
        var g = this.TryGet().Result.SptStorage.TryGet().Result;
        if (g is null)
        {
            return;
        }

        foreach (var x in g.IdChain)
        {
            x.AddToDictionary(dic);
        }
    }
}

public class StoragePointTest3
{
    [Fact]
    public async Task Test0()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<StoragePoint<SptClass>>(true);
        var c1 = await crystal.Data.PinData();
        // var c1 = new SptClass();
        // crystal.Data.Set(c1);

        c1.TryInitialize(1, "Root", "R", []); // 1 Root R []

        await crystal.Crystalizer.StoreAndRelease();

        c1 = await crystal.Data.TryGet();
        c1.Name = "Nuu";

        await crystal.Crystalizer.StoreAndRelease();
        (await crystal.Crystalizer.TestJournalAll()).IsTrue();

        await TestHelper.StoreAndReleaseAndDelete(crystal);
    }

    // [Fact]
    public async Task Test1()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<StoragePoint<SptClass>>(true);
        // var c1 = crystal.Data;
        var c1 = await crystal.Data.PinData();
        // var c1 = (await s1.TryLock()).Data!;
        // s1.Unlock();

        await this.Setup(c1);
        await this.Validate1(c1);
        await this.Modify1(c1);
        await this.Validate2(c1);

        await crystal.Crystalizer.StoreAndRelease(); // await crystal.Store(StoreMode.ForceRelease); await crystal.Crystalizer.StoreJournal();

        await this.Validate2(c1);
        await this.Modify2(c1);
        await this.Validate3(c1);

        await crystal.Crystalizer.StoreAndRelease(); // await crystal.Store(StoreMode.ForceRelease); await crystal.Crystalizer.StoreJournal();
        (await crystal.Crystalizer.TestJournalAll()).IsTrue();

        await TestHelper.StoreAndReleaseAndDelete(crystal);
    }

    private async Task Setup(SptClass c1)
    {
        c1.TryInitialize(1, "Root", "R", []); // 1 Root R []

        var c2 = c1.Add(2, "Nu", "Po", [3, 4]); // 2 Nu Po [3,4]
        var c21 = c2.Add(21, "Nu", "Poo", [10]); // 21 Nu Poo [10]
        var c22 = c2.Add(22, "Nu", "Pooo", [20]); // 22 Nu Poo [20]

        var c3 = c1.Add(3, "Ku", "Po", [5, 6, 7]); // 3 Ku Po [5,6,7]
        var c31 = c3.Add(31, "Ku", "Pa", [30]); // 31 Ku Pa [30]
        var c32 = c3.Add(32, "Ku", "Paa", [31]); // 32 Ku Paa [31]
        var c33 = c3.Add(33, "Ku", "Paaa", [32]); // 33 Ku Paaa [32]
        var c300 = c31.Add(300, "Nu", "Pa", [300]); // 300 Nu Pa [300]

        var c4 = c1.Add(4, "Do", "Ra", [8, 9]); // 4 Do Ra [8,9]
        var c41 = c4.Add(41, "Do", "Rara", [1, 2, 3]); // 41 Do Rara [1,2,3]
        var c42 = c4.Add(42, "Do", "Rarara", [4, 5, 6]); // 42 Do Rarara [4,5,6]
        var c43 = c4.Add(43, "Do", "Rararara", [7, 8, 9]); // 43 Do Rararara [7,8,9]

        var c51 = c42.Add(51, "O", "Ra", [1, 2]); // 51 O Ra [1,2]
        var c52 = c42.Add(52, "O", "Rara", [3, 4]); // 52 O Rara [3, 4]
    }

    private async Task Setup(SptPoint c1)
    {
        c1.TryInitialize(1, "Root", "R", []); // 1 Root R []

        var c2 = c1.Add(2, "Nu", "Po", [3, 4]); // 2 Nu Po [3,4]
        var c21 = c2.Add(21, "Nu", "Poo", [10]); // 21 Nu Poo [10]
        var c22 = c2.Add(22, "Nu", "Pooo", [20]); // 22 Nu Poo [20]

        var c3 = c1.Add(3, "Ku", "Po", [5, 6, 7]); // 3 Ku Po [5,6,7]
        var c31 = c3.Add(31, "Ku", "Pa", [30]); // 31 Ku Pa [30]
        var c32 = c3.Add(32, "Ku", "Paa", [31]); // 32 Ku Paa [31]
        var c33 = c3.Add(33, "Ku", "Paaa", [32]); // 33 Ku Paaa [32]
        var c300 = c31.Add(300, "Nu", "Pa", [300]); // 300 Nu Pa [300]

        var c4 = c1.Add(4, "Do", "Ra", [8, 9]); // 4 Do Ra [8,9]
        var c41 = c4.Add(41, "Do", "Rara", [1, 2, 3]); // 41 Do Rara [1,2,3]
        var c42 = c4.Add(42, "Do", "Rarara", [4, 5, 6]); // 42 Do Rarara [4,5,6]
        var c43 = c4.Add(43, "Do", "Rararara", [7, 8, 9]); // 43 Do Rararara [7,8,9]

        var c51 = c42.Add(51, "O", "Ra", [1, 2]); // 51 O Ra [1,2]
        var c52 = c42.Add(52, "O", "Rara", [3, 4]); // 52 O Rara [3, 4]
    }

    private async Task Validate1(SptClass c1)
    {
        var dic = c1.ToDictionary();
        dic[1].Validate(1, "Root", "R", []);
        dic[1].ValidateChildren([2, 3, 4]);

        dic[2].Validate(2, "Nu", "Po", [3, 4]);
        dic[2].ValidateChildren([21, 22]);

        dic[3].Validate(3, "Ku", "Po", [5, 6, 7]);
        dic[3].ValidateChildren([31, 32, 33]);

        dic[4].Validate(4, "Do", "Ra", [8, 9]);
        dic[4].ValidateChildren([41, 42, 43]);

        dic[21].Validate(21, "Nu", "Poo", [10]);
        dic[22].Validate(22, "Nu", "Pooo", [20]);

        dic[31].Validate(31, "Ku", "Pa", [30]);
        dic[32].Validate(32, "Ku", "Paa", [31]);
        dic[33].Validate(33, "Ku", "Paaa", [32]);

        dic[300].Validate(300, "Nu", "Pa", [300]);

        dic[41].Validate(41, "Do", "Rara", [1, 2, 3]);
        dic[42].Validate(42, "Do", "Rarara", [4, 5, 6]);
        dic[42].ValidateChildren([51, 52]);
        dic[43].Validate(43, "Do", "Rararara", [7, 8, 9]);

        dic[51].Validate(51, "O", "Ra", [1, 2]);
        dic[52].Validate(52, "O", "Rara", [3, 4]);
    }

    private async Task Modify1(SptClass c1)
    {
        var points = c1.ToPoints();
        using (var dataScope = await points[2].TryLock())
        {
            if (dataScope.IsValid)
            {
                dataScope.Data.Name = "Nuuu";
                using (var dataScope2 = await dataScope.Data.SptStorage.TryLock(23, AcquisitionMode.GetOrCreate))
                {// Add 23 Nu Poooo [30]
                    dataScope2.IsValid.IsTrue();
                    dataScope2.Data.TryInitialize(23, "Nu", "Poooo", [30]);
                    using (var dataScope3 = await dataScope2.Data.SptStorage.TryLock(30, AcquisitionMode.GetOrCreate))
                    {// Add 30 Nu Pu []
                        dataScope3.IsValid.IsTrue();
                        dataScope3.Data.TryInitialize(30, "Nu", "Pu", []);
                    }
                }

                (await dataScope.Data.SptStorage.Delete(21)).Is(DataScopeResult.Success);
            }
        }

        await points[22].DeleteData();
        points[21].ValidateDeleted();
        points[22].ValidateDeleted();
        var sp = points[2].TryGet().Result.SptStorage.TryGet().Result;
        sp.IdChain.ContainsKey(21).IsFalse();
        sp.IdChain.ContainsKey(22).IsFalse();

        await points[31].DeleteData();
        points[3].TryGet().Result.ValidateChildren([32, 33]);
        points[300].ValidateDeleted();
        using (var dataScope = await points[32].TryLock())
        {
            dataScope.IsValid.IsTrue();
            dataScope.Data.SptInt.Add(new(41));
        }
    }

    private async Task Validate2(SptClass c1)
    {
        var dic = c1.ToDictionary();
        dic[1].Validate(1, "Root", "R", []);
        dic[1].ValidateChildren([2, 3, 4]);

        dic[2].Validate(2, "Nuuu", "Po", [3, 4]);
        dic[2].ValidateChildren([23]);

        dic[3].Validate(3, "Ku", "Po", [5, 6, 7]);
        dic[3].ValidateChildren([32, 33]);

        dic[4].Validate(4, "Do", "Ra", [8, 9]);
        dic[4].ValidateChildren([41, 42, 43]);

        dic.ContainsKey(21).IsFalse();
        dic.ContainsKey(22).IsFalse();
        dic[23].Validate(23, "Nu", "Poooo", [30]);
        dic[30].Validate(30, "Nu", "Pu", []);

        dic.ContainsKey(31).IsFalse();
        dic[32].Validate(32, "Ku", "Paa", [31, 41]);
        dic[33].Validate(33, "Ku", "Paaa", [32]);

        dic.ContainsKey(300).IsFalse();

        dic[41].Validate(41, "Do", "Rara", [1, 2, 3]);
        dic[42].Validate(42, "Do", "Rarara", [4, 5, 6]);
        dic[42].ValidateChildren([51, 52]);
        dic[43].Validate(43, "Do", "Rararara", [7, 8, 9]);

        dic[51].Validate(51, "O", "Ra", [1, 2]);
        dic[52].Validate(52, "O", "Rara", [3, 4]);
    }

    private async Task Modify2(SptClass c1)
    {
        var points = c1.ToPoints();

        await points[42].DeleteData();
        points[42].ValidateDeleted();
        points[51].ValidateDeleted();
        points[52].ValidateDeleted();

        using (var dataScope = await points[32].TryLock())
        {
            dataScope.IsValid.IsTrue();
            dataScope.Data.SptInt.Add(new(51));
        }
    }

    private async Task Validate3(SptClass c1)
    {
        var dic = c1.ToDictionary();
        dic[1].Validate(1, "Root", "R", []);
        dic[1].ValidateChildren([2, 3, 4]);

        dic[2].Validate(2, "Nuuu", "Po", [3, 4]);
        dic[2].ValidateChildren([23]);

        dic[3].Validate(3, "Ku", "Po", [5, 6, 7]);
        dic[3].ValidateChildren([32, 33]);

        dic[4].Validate(4, "Do", "Ra", [8, 9]);
        dic[4].ValidateChildren([41, 43]);

        dic.ContainsKey(21).IsFalse();
        dic.ContainsKey(22).IsFalse();
        dic[23].Validate(23, "Nu", "Poooo", [30]);
        dic[30].Validate(30, "Nu", "Pu", []);

        dic.ContainsKey(31).IsFalse();
        dic[32].Validate(32, "Ku", "Paa", [31, 41, 51]);
        dic[33].Validate(33, "Ku", "Paaa", [32]);

        dic.ContainsKey(300).IsFalse();

        dic[41].Validate(41, "Do", "Rara", [1, 2, 3]);
        dic.ContainsKey(42).IsFalse();
        dic[43].Validate(43, "Do", "Rararara", [7, 8, 9]);

        dic.ContainsKey(51).IsFalse();
        dic.ContainsKey(52).IsFalse();
    }

    private async Task Validate1(SptPoint s1)
    {
        s1.Validate(1, "Root", "R", []);
        var dic = s1.ToDictionary();
        dic[2].Validate(2, "Nu", "Po", [3, 4]);
        dic[3].Validate(3, "Ku", "Po", [5, 6, 7]);
        dic[4].Validate(4, "Do", "Ra", [8, 9]);

        dic[21].Validate(21, "Nu", "Poo", [10]);
        dic[22].Validate(22, "Nu", "Pooo", [20]);

        dic[31].Validate(31, "Ku", "Pa", [30]);
        dic[32].Validate(32, "Ku", "Paa", [31]);
        dic[33].Validate(33, "Ku", "Paaa", [32]);

        dic[300].Validate(300, "Nu", "Pa", [300]);

        dic[41].Validate(41, "Do", "Rara", [1, 2, 3]);
        dic[42].Validate(42, "Do", "Rarara", [4, 5, 6]);
        dic[43].Validate(43, "Do", "Rararara", [7, 8, 9]);

        dic[51].Validate(51, "O", "Ra", [1, 2]);
        dic[52].Validate(52, "O", "Rara", [3, 4]);
    }
}
