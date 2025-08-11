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
    [Key(0)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    public int Id { get; set; }

    [Key(1)]
    public StoragePoint<FirstData> FirstDataStorage { get; set; } = new();
}

// First, create a class to represent the data content.
[TinyhandObject(Structual = true)] // Annotate TinyhandObject attribute to make this class serializable.
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

internal class Program
{
    public static async Task Main(string[] args)
    {
        // Create a builder to organize dependencies and register data configurations.
        var builder = new CrystalControl.Builder()
            .Configure(context =>
            {
                // context.AddTransient<FirstData>();
                context.AddSingleton<FirstData>();
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
            });

        var unit = builder.Build(); // Build.
        TinyhandSerializer.ServiceProvider = unit.Context.ServiceProvider;
        var crystalizer = unit.Context.ServiceProvider.GetRequiredService<Crystalizer>(); // Obtains a Crystalizer instance for data storage operations.
        await crystalizer.PrepareAndLoadAll(true); // Prepare resources for storage operations and read data from files.

        var data = unit.Context.ServiceProvider.GetRequiredService<FirstData>(); // Retrieve a data instance from the service provider.

        Console.WriteLine($"Load {data.ToString()}");
        data.Id += 1;
        Console.WriteLine($"Save {data.ToString()}");

        var crystal = unit.Context.ServiceProvider.GetRequiredService<ICrystal<FirstData>>();
        data = crystal.Data;
        data.Id += 1;
        Console.WriteLine($"Crystal {data.ToString()}");

        await crystal.Save(UnloadMode.ForceUnload);
        Console.WriteLine($"Unload {crystal.Data.ToString()}");
        crystal.Data.Id++;
        Console.WriteLine($"Unload++ {crystal.Data.ToString()}");

        data = unit.Context.ServiceProvider.GetRequiredService<FirstData>();
        Console.WriteLine($"Data {data.ToString()}");

        crystal = unit.Context.ServiceProvider.GetRequiredService<ICrystal<FirstData>>();
        Console.WriteLine($"Crystal {crystal.Data.ToString()}");

        await crystalizer.SaveAll(); // Save all data.
    }
}
