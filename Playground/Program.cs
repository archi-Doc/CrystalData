// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Arc.Threading;
using Arc.Unit;
using CrystalData;
using Microsoft.Extensions.DependencyInjection;
using Tinyhand;
using Tinyhand.IO;
using ValueLink;

namespace Sandbox;

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.Serializable)]
public partial record SpSecondClass
{
    public SpSecondClass(int id)
    {
        this.Id = id;
    }

    [Key(0)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    public int Id { get; set; }

    [Key(1)]
    public StoragePoint<FirstData> FirstDataStorage { get; set; } = new();
}

[TinyhandObject(Structual = true)]
public partial class FirstData
{
    public FirstData()
    {
    }

    [Key(0)] // The key attribute specifies the index at serialization
    public int Id { get; set; }

    [Key(1)]
    [DefaultValue("Hoge")] // The default value for the name property.
    public string Name { get; set; } = string.Empty;

    [Key(2)]
    public StoragePoint<int> IntStorage { get; set; } = new();

    public override string ToString()
        => $"Id: {this.Id}, Name: {this.Name}";
}

[TinyhandObject(Structual = true)]
// [TinyhandObject(Structual = true, UseServiceProvider = true)]
public partial class SecondData
{
    public SecondData()
    {
    }

    [Key(0)]
    public StoragePoint<double> DoubleStorage { get; set; } = new();

    [Key(1)]
    public SpClassPoint.GoshujinClass SpClassGoshujin { get; set; } = new();

    public override string ToString()
        => $"Second: {this.DoubleStorage.GetOrCreate()}";
}

/// <summary>
/// A class at the isolation level of StoragePoint that inherits from <see cref="StoragePoint{TData}" />.
/// It maintains relationship information between classes and owns data of type TData as storage.
/// </summary>
[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.ReadCommitted)]
public partial class SpClassPoint : StoragePoint<SpClass>
{// Value, Link
    [Key(1)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    public int Id { get; set; }

    public void Test()
    {
        this.TryLock();
    }
}

[TinyhandObject(Structual = true)]
public partial class SpClass
{
    public SpClass()
    {
    }

    [Key(0)]
    public string Name { get; set; } = string.Empty;
}

internal class Program
{
    public static async Task Main(string[] args)
    {
        // Create a builder to organize dependencies and register data configurations.
        var builder = new CrystalControl.Builder()
            .Configure(context =>
            {
                // context.AddSingleton<FirstData>();
                context.AddSingleton<SecondData>();
            })
            .SetupOptions<CrystalizerOptions>((context, options) =>
            {
                context.GetOptions<UnitOptions>(out var unitOptions);
                if (unitOptions is not null)
                {
                    options.GlobalDirectory = new LocalDirectoryConfiguration(Path.Combine(unitOptions.DataDirectory, "Global"));
                }
            })
            .ConfigureCrystal(context =>
            {
                // context.SetJournal(new SimpleJournalConfiguration(new GlobalDirectoryConfiguration("Journal")));

                // Register FirstData configuration.
                context.AddCrystal<FirstData>(
                    new CrystalConfiguration()
                    {
                        RequiredForLoading = true,
                        SavePolicy = SavePolicy.Manual, // The timing of saving data is controlled by the application.
                        SaveFormat = SaveFormat.Utf8, // The format is utf8 text.
                        NumberOfFileHistories = 0, // No history file.
                        // FileConfiguration = new LocalFileConfiguration("Local/SimpleExample/SimpleData.tinyhand"), // Specify the file name to save.
                        FileConfiguration = new GlobalFileConfiguration(), // Specify the file name to save.
                        StorageConfiguration = new SimpleStorageConfiguration(
                            new GlobalDirectoryConfiguration("MainStorage"),
                            new GlobalDirectoryConfiguration("BackupStorage")),
                    });

                context.AddCrystal<SecondData>(
                    new CrystalConfiguration()
                    {
                        SavePolicy = SavePolicy.Manual, // The timing of saving data is controlled by the application.
                        SaveFormat = SaveFormat.Utf8, // The format is utf8 text.
                        NumberOfFileHistories = 0, // No history file.
                        FileConfiguration = new GlobalFileConfiguration(), // Specify the file name to save.
                        StorageConfiguration = new SimpleStorageConfiguration(
                            new GlobalDirectoryConfiguration("MainStorage"),
                            new GlobalDirectoryConfiguration("BackupStorage")) with
                        {
                            NumberOfHistoryFiles = 2,
                        },
                    });
            });

        var unit = builder.Build(); // Build.
        TinyhandSerializer.ServiceProvider = unit.Context.ServiceProvider;
        var crystalizer = unit.Context.ServiceProvider.GetRequiredService<Crystalizer>(); // Obtains a Crystalizer instance for data storage operations.
        await crystalizer.PrepareAndLoadAll(true); // Prepare resources for storage operations and read data from files.

        var data = unit.Context.ServiceProvider.GetRequiredService<FirstData>();

        Console.WriteLine($"Load {data.ToString()}");
        data.Id += 1;
        Console.WriteLine($"Save {data.ToString()}");

        var crystal = unit.Context.ServiceProvider.GetRequiredService<ICrystal<FirstData>>();
        data = crystal.Data;
        data.Id += 1;
        Console.WriteLine($"Crystal {data.ToString()}");

        await crystal.Store(StoreMode.ForceRelease);
        Console.WriteLine($"Unload {crystal.Data.ToString()}");
        crystal.Data.Id++;
        Console.WriteLine($"Unload++ {crystal.Data.ToString()}");

        data = unit.Context.ServiceProvider.GetRequiredService<FirstData>();
        Console.WriteLine($"Data {data.ToString()}");

        crystal = unit.Context.ServiceProvider.GetRequiredService<ICrystal<FirstData>>();
        Console.WriteLine($"Crystal {crystal.Data.ToString()}");

        var data2 = unit.Context.ServiceProvider.GetRequiredService<SecondData>();
        var doubleStorage = data2.DoubleStorage;
        var d = await doubleStorage.GetOrCreate();
        data.IntStorage.Set(await data.IntStorage.GetOrCreate() + 1);
        data2.DoubleStorage.Set(await data2.DoubleStorage.GetOrCreate() + 1.2);
        Console.WriteLine($"First: {await data.IntStorage.GetOrCreate()}");
        Console.WriteLine($"Second: {await data2.DoubleStorage.GetOrCreate()}");

        var spClassGoshujin = data2.SpClassGoshujin;
        using (var scope = await spClassGoshujin.TryLock(1, LockMode.GetOrCreate))
        {
            if (scope.Data is { } spClass)
            {
                spClass.Name += "ox";
                Console.WriteLine(spClass.Name);
            }
        }

        spClassGoshujin.Delete(1);

        //await crystalizer.Store(); // Save all data.
        await crystalizer.StoreAndRelease();
        Console.WriteLine($"MemoryUsage: {crystalizer.StorageControl.MemoryUsage}");
    }
}
