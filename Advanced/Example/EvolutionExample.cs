// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace QuickStart.Evolution;

#pragma warning disable SA1204 // Static elements should appear before instance elements
#pragma warning disable SA1402 // File may only contain a single type

[TinyhandObject(LockObject = "syncObject")]
[ValueLinkObject]
public partial class Class1
{
    [Key(0)]
    [Link(Primary = true, Unique = true, Type = ChainType.Unordered)]
    public int Id { get; set; }

    private readonly Lock syncObject = new();

    public Class1(int id)
    {
        this.Id = id;
    }

    public void Test()
    {
        using (this.syncObject.EnterScope())
        {
            this.Id++;
            Console.WriteLine($"Class1: {this}");
        }
    }

    public override string ToString()
        => this.Id.ToString();
}

[TinyhandObject(Structural = true)]
public partial class Class2
{
    [Key(0)]
    public StoragePoint<Class1> Member1 { get; set; } = new();

    [Key(1)]
    public StoragePoint<byte[]> Member2 { get; set; } = new();

    public async Task Test()
    {
        using (var dataScope = await this.Member1.TryLock())
        {
            if (dataScope.IsValid)
            {
                dataScope.Data.Id++;
            }
        }

        var data = await this.Member2.TryGet();
        var newData = new byte[(data is null ? 0 : data.Length) + 100];
        this.Member2.Set(newData);

        var class1 = await this.Member1.TryGet();
        data = await this.Member2.TryGet();
        Console.WriteLine($"Class2: {class1?.Id},{(data is null ? 0 : data.Length)}");
    }
}

[TinyhandObject]
public partial class Class3
{
    [Key(0)]
    public Class1.GoshujinClass Goshujin { get; set; } = new();

    public void Test()
    {
        var c = new Class1(this.Goshujin.Count);
        c.Goshujin = this.Goshujin;

        Console.WriteLine($"Class3: {string.Join(',', this.Goshujin.Select(x => x.ToString()))}");
    }
}

[TinyhandObject(Structural = true)]
[ValueLinkObject(Isolation = IsolationLevel.ReadCommitted)]
public partial class Class1Point : StoragePoint<Class1>
{
    [Key(1)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    public int Id { get; private set; }

    public Class1Point(int id)
    {
    }
}

[TinyhandObject]
public partial class Class4
{
    [Key(0)]
    public Class1Point.GoshujinClass Goshujin { get; set; } = new();

    public async Task Test()
    {
        var count = this.Goshujin.Count;
        using (var dataScope = await this.Goshujin.TryLock(count, AcquisitionMode.GetOrCreate))
        {
            if (dataScope.IsValid)
            {
            }
        }

        var ids = this.Goshujin.IdChain.Keys;
        Console.WriteLine($"Class4: {string.Join(',', ids.Select(x => x.ToString()))}");
    }
}

public class EvolutionExample
{
    private readonly CrystalControl crystalControl;
    private readonly Class1 class1;
    private readonly Class2 class2;
    private readonly Class3 class3;
    private readonly Class4 class4;

    public EvolutionExample(CrystalControl crystalControl, Class1 class1, Class2 class2, Class3 class3, Class4 class4)
    {
        this.crystalControl = crystalControl;
        this.class1 = class1;
        this.class2 = class2;
        this.class3 = class3;
        this.class4 = class4;
    }

    public async Task Process()
    {
        // this.class1.Test();
        // await this.class2.Test();
        // this.class3.Test();
        await this.class4.Test();
    }

    public static async Task<UnitProduct?> Program()
    {
        var myBuilder = new UnitBuilder();

        var crystalDataBuilder = new CrystalUnit.Builder()
            .Configure(context =>
            {
                context.TryAddSingleton<EvolutionExample>(); // Register SecondExample class.
            })
            .ConfigureCrystal(context =>
            {
                var storageConfiguration = new SimpleStorageConfiguration(new LocalDirectoryConfiguration("Local/EvolutionExample/Storage"));

                context.AddCrystal<Class1>(
                    new CrystalConfiguration()
                    {
                        SaveFormat = SaveFormat.Utf8, // Format is utf8 text.
                        FileConfiguration = new LocalFileConfiguration("Local/EvolutionExample/Class1.tinyhand"), // Specify the file name to save.
                        NumberOfFileHistories = 0,
                    });

                context.AddCrystal<Class2>(
                    new CrystalConfiguration()
                    {
                        SaveFormat = SaveFormat.Utf8, // Format is utf8 text.
                        FileConfiguration = new LocalFileConfiguration("Local/EvolutionExample/Class2.tinyhand"), // Specify the file name to save.
                        NumberOfFileHistories = 0,
                        StorageConfiguration = storageConfiguration,
                    });

                context.AddCrystal<Class3>(
                    new CrystalConfiguration()
                    {
                        SaveFormat = SaveFormat.Utf8, // Format is utf8 text.
                        FileConfiguration = new LocalFileConfiguration("Local/EvolutionExample/Class3.tinyhand"), // Specify the file name to save.
                        NumberOfFileHistories = 0,
                    });

                context.AddCrystal<Class4>(
                    new CrystalConfiguration()
                    {
                        SaveFormat = SaveFormat.Utf8, // Format is utf8 text.
                        FileConfiguration = new LocalFileConfiguration("Local/EvolutionExample/Class4.tinyhand"), // Specify the file name to save.
                        NumberOfFileHistories = 0,
                        StorageConfiguration = storageConfiguration,
                    });
            });

        myBuilder.AddBuilder(crystalDataBuilder);

        var unit = myBuilder.Build(); // Build.
        TinyhandSerializer.ServiceProvider = unit.Context.ServiceProvider;
        var crystalControl = unit.Context.ServiceProvider.GetRequiredService<CrystalControl>();
        var result = await crystalControl.PrepareAndLoad(true); // Use the default query.
        if (result.IsFailure())
        {// Abort
            return default;
        }

        var example = unit.Context.ServiceProvider.GetRequiredService<EvolutionExample>();
        await example.Process();

        return unit;
    }
}
