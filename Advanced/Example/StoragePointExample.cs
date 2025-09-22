// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace QuickStart;

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.Serializable)]
public partial class SptClass
{
    [Key(0)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    public int Id { get; private set; }

    [Key(1)]
    public string Name { get; private set; } = string.Empty;

    [Key(2)]
    public int Count { get; set; }

    [Key(3)]
    public StoragePoint<SptPoint.GoshujinClass> SptStorage { get; set; } = new();

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

    public override string ToString()
        => $"Id: {this.Id}, Name: {this.Name}, Count: {this.Count}";
}

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.ReadCommitted)]
public partial class SptPoint : StoragePoint<SptClass>
{
    [Key(1)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    public int Id { get; set; }
}

public class StoragePointExample
{
    public StoragePointExample(CrystalControl crystalControl, ICrystal<SptClass> crystal)
    {
        this.crystalControl = crystalControl;
        this.crystal = crystal; // Get an ICrystal interface for data storage operations.
    }

    public async Task Process()
    {
        var c1 = this.crystal.Data;
        c1.TryInitialize(1, "One"); // Initialize 1 "One"
        Console.WriteLine(c1.ToString());

        using (var dataScope = await c1.SptStorage.TryLock(2, AcquisitionMode.GetOrCreate))
        {// Create 2 "Two"
            if (dataScope.IsValid)
            {
                var c2 = dataScope.Data;
                c2.TryInitialize(2, "Two");
                c2.Count++;
                Console.WriteLine(c2.ToString());
            }
        }
    }

    private readonly CrystalControl crystalControl;
    private readonly ICrystal<SptClass> crystal;
}

public class SptRoot
{
    public SptClass.GoshujinClass Data1 { get; set; } = new();

    public StoragePoint<SptClass.GoshujinClass> Data2 { get; set; } = new();

    public SptPoint.GoshujinClass Data3 { get; set; } = new();

    public StoragePoint<SptPoint.GoshujinClass> Data4 { get; set; } = new();
}

public partial class Program
{
    public static async Task<BuiltUnit?> StoragePointExample()
    {
        var builder = new CrystalUnit.Builder()
            .Configure(context =>
            {
                context.AddSingleton<StoragePointExample>();
            })
            .ConfigureCrystal(context =>
            {
                context.SetJournal(new SimpleJournalConfiguration(new LocalDirectoryConfiguration("Local/StoragePointExample/Journal")));
                context.AddCrystal<SptClass>(
                    new(new LocalFileConfiguration("Local/StoragePointExample/SptClass.tinyhand"))
                    {
                        SaveFormat = SaveFormat.Utf8,
                        NumberOfFileHistories = 3,
                        StorageConfiguration = new SimpleStorageConfiguration(new LocalDirectoryConfiguration("Local/StoragePointExample/Storage"))
                        {
                            NumberOfHistoryFiles = 3,
                        },
                    });
            });

        var product = builder.Build();
        TinyhandSerializer.ServiceProvider = product.Context.ServiceProvider;
        var crystalControl = product.Context.ServiceProvider.GetRequiredService<CrystalControl>();
        var result = await crystalControl.PrepareAndLoad(true); // Use the default query.
        if (result.IsFailure())
        {// Abort
            return default;
        }

        var example = product.Context.ServiceProvider.GetRequiredService<StoragePointExample>();
        await example.Process();

        return product;
    }
}
