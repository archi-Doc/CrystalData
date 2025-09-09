// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace QuickStart;

public class ConfigurationExampleClass
{
    public ConfigurationExampleClass(Crystalizer crystalizer, FirstData firstData)
    {
        this.crystalizer = crystalizer;
        this.firstData = firstData;
    }

    public async Task Process()
    {
        Console.WriteLine($"First: {this.firstData.ToString()}");
        this.firstData.Id += 2;
        Console.WriteLine($"First: {this.firstData.ToString()}");

        // Get or create an ICrystal interface of the data.
        var crystal = this.crystalizer.GetOrCreateCrystal<SecondData>(
            new CrystalConfiguration(
                SavePolicy.Manual,
                new LocalFileConfiguration("Local/ConfigurationTimingExample/SecondData.tinyhand")));
        var secondData = crystal.Data;

        Console.WriteLine($"Second: {secondData.ToString()}");
        secondData.Id += 1;
        Console.WriteLine($"Second: {secondData.ToString()}");

        // You can create multiple crystals from single data class.
        var crystal2 = this.crystalizer.CreateCrystal<SecondData>(
            new CrystalConfiguration(
                SavePolicy.Manual,
                new LocalFileConfiguration("Local/ConfigurationTimingExample/SecondData2.tinyhand")));
        var secondData2 = crystal2.Data;

        Console.WriteLine($"Second: {secondData2.ToString()}");
        secondData2.Id += 3;
        Console.WriteLine($"Second: {secondData2.ToString()}");
    }

    private readonly Crystalizer crystalizer;
    private FirstData firstData;
}

public partial class Program
{
    public static async Task<BuiltUnit> ConfigurationExample()
    {
        var builder = new CrystalControl.Builder()
            .Configure(context =>
            {
                context.AddSingleton<ConfigurationExampleClass>();
            })
            .ConfigureCrystal(context =>
            {
                // Register SimpleData configuration.
                context.AddCrystal<FirstData>(
                    new CrystalConfiguration()
                    {
                        SavePolicy = SavePolicy.Manual, // Timing of saving data is controlled by the application.
                        SaveFormat = SaveFormat.Utf8, // Format is utf8 text.
                        NumberOfFileHistories = 0, // No history file.
                        FileConfiguration = new LocalFileConfiguration("Local/ConfigurationTimingExample/FirstData.tinyhand"), // Specify the file name to save.
                    });
            });

        var unit = builder.Build(); // Build.
        var crystalizer = unit.Context.ServiceProvider.GetRequiredService<Crystalizer>(); // Obtains a Crystalizer instance for data storage operations.
        await crystalizer.PrepareAndLoad(); // Prepare resources for storage operations and read data from files.

        var example = unit.Context.ServiceProvider.GetRequiredService<ConfigurationExampleClass>();
        await example.Process();

        return unit;
    }
}
