// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1307
#pragma warning disable SA1401

namespace QuickStart;

[TinyhandObject(Structual = true)] // Journaling feature is necessary to allow the function to save data when properties are changed.
public partial class SaveTimingData
{
    [Key(0, AddProperty = "Id")] // Add a property to save data when the value is changed.
    internal int id;

    public override string ToString()
        => $"Id: {this.Id}";
}

public partial class Program
{
    public static async Task<BuiltUnit> SaveTimingExample()
    {
        // Create a builder to organize dependencies and register data configurations.
        var builder = new CrystalControl.Builder()
            .ConfigureCrystal(context =>
            {
                // Register SimpleData configuration.
                context.AddCrystal<SaveTimingData>(
                    new CrystalConfiguration()
                    {
                        SavePolicy = SavePolicy.Periodic, // Data will be saved at regular intervals.
                        SaveInterval = TimeSpan.FromMinutes(1), // The interval at which data is stored.
                        SaveFormat = SaveFormat.Utf8, // Format is utf8 text.
                        NumberOfFileHistories = 0, // No history file.
                        FileConfiguration = new LocalFileConfiguration("Local/SaveTimingExample/SaveTimingData.tinyhand"), // Specify the file name to save.
                    });
            });

        var unit = builder.Build(); // Build.
        var crystalizer = unit.Context.ServiceProvider.GetRequiredService<Crystalizer>(); // Obtains a Crystalizer instance for data storage operations.
        await crystalizer.Prepare(true, false); // Prepare resources for storage operations and read data from files.

        var crystal = unit.Context.ServiceProvider.GetRequiredService<ICrystal<SaveTimingData>>();
        var data = crystal.Data;

        // Save instantly
        data.id += 1;
        await crystal.Store();

        // On changed
        data.Id += 2; // Add to the save queue when the value is changed

        // On changed - alternative
        data.id += 2;
        crystal.AddToSaveQueue();

        // Manual...
        await crystal.Store();

        return unit;
    }
}
