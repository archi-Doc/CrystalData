﻿// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.ComponentModel;

namespace QuickStart;

[TinyhandObject(UseServiceProvider = true)] // Annotate TinyhandObject attribute to make this class serializable.
public partial class ServiceProviderData
{
    public ServiceProviderData()
    {
    }

    [Key(0)] // The key attribute specifies the index at serialization
    public double Age { get; set; }

    public override string ToString()
        => $"Age: {this.Age.ToString()}";
}

public partial class Program
{
    public static async Task<BuiltUnit> ServiceProviderExample()
    {
        // Create a builder to organize dependencies and register data configurations.
        var builder = new CrystalControl.Builder()
            .Configure(context =>
            {
                context.AddTransient<ServiceProviderData>();
            })
            .ConfigureCrystal(context =>
            {
                // Register SimpleData configuration.
                context.AddCrystal<ServiceProviderData>(
                    new CrystalConfiguration()
                    {
                        SavePolicy = SavePolicy.Manual, // Timing of saving data is controlled by the application.
                        SaveFormat = SaveFormat.Utf8, // Format is utf8 text.
                        NumberOfFileHistories = 0, // No history file.
                        FileConfiguration = new LocalFileConfiguration("Local/ServiceProviderExample/ServiceProviderExample.tinyhand"), // Specify the file name to save.
                    });
            });

        var unit = builder.Build(); // Build.
        TinyhandSerializer.ServiceProvider = unit.Context.ServiceProvider;
        var crystalizer = unit.Context.ServiceProvider.GetRequiredService<Crystalizer>(); // Obtains a Crystalizer instance for data storage operations.
        await crystalizer.PrepareAndLoadAll(false); // Prepare resources for storage operations and read data from files.

        var data = unit.Context.ServiceProvider.GetRequiredService<ICrystal<ServiceProviderData>>().Data; // Retrieve a data instance from the service provider.

        Console.WriteLine($"Load {data.ToString()}");
        data.Age += 1000d;
        Console.WriteLine($"Save {data.ToString()}");

        await crystalizer.SaveAll(); // Save all data.

        return unit;
    }
}
