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
    public string Name = string.Empty;

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
        using (var dataScope = this.TryLock(AcquisitionMode.GetOrCreate).Result)
        {
            if (dataScope.IsValid)
            {
                using (var dataScope2 = dataScope.Data.SptStorage.TryLock(id, AcquisitionMode.GetOrCreate).Result)
                {
                    if (dataScope2.IsValid)
                    {
                        dataScope2.Data.TryInitialize(id, name, text, numbers);
                        return dataScope2.Data;
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
            }
            else
            {
                throw new Exception();
                // return default;
            }
        }
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
    public async Task Test1()
    {
        var crystal = await TestHelper.CreateAndStartCrystal<StoragePoint<SptClass>>(true);
        // var c1 = crystal.Data;
        var c1 = await crystal.Data.PinData();
        // var c1 = (await s1.TryLock()).Data!;
        // s1.Unlock();

        await this.Setup(c1);
        await this.Validate1(c1);

        await crystal.Store(StoreMode.ForceRelease);
        await crystal.Crystalizer.StoreJournal();

        await this.Validate1(c1);
        await this.Modify2(c1);

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

    private async Task Modify2(SptClass c1)
    {
        var points = c1.ToPoints();
    }

    private async Task Validate1(SptClass c1)
    {
        var dic = c1.ToDictionary();
        dic[1].Validate(1, "Root", "R", []);
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
