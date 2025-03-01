// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace QuickStart;

public partial class Program
{
    private const string BucketName = "test_bucket";
    private const string KeyPair = "AccessKeyId=SecretAccessKey";

    public static async Task<BuiltUnit> PathExample()
    {
        // Create a builder to organize dependencies and register data configurations.
        var builder = new CrystalControl.Builder()
            .ConfigureCrystal(context =>
            {
            })
            .SetupOptions<CrystalizerOptions>((context, options) =>
            {// You can change the root directory of the CrystalData by modifying CrystalizerOptions.
                context.GetOptions<UnitOptions>(out var unitOptions); // Get the application root directory.
                if (unitOptions is not null)
                {
                    // options.RootPath = Path.Combine(unitOptions.RootDirectory, "Additional"); // Root directory
                    options.GlobalDirectory = new LocalDirectoryConfiguration(Path.Combine(unitOptions.DataDirectory, "Global")); // Global directory
                }
            });

        var unit = builder.Build(); // Build.
        var crystalizer = unit.Context.ServiceProvider.GetRequiredService<Crystalizer>(); // Obtains a Crystalizer instance for data storage operations.

        // Get the crystal from crystalizer.
        var crystal = crystalizer.GetOrCreateCrystal<FirstData>(
            new CrystalConfiguration()
            {
                SaveFormat = SaveFormat.Utf8, // Format is utf8 text.

                // If a relative path is specified, it combines the root directory of Crystalizer with the path to create an absolute path.
                FileConfiguration = new LocalFileConfiguration("Local/PathExample/FirstData.tinyhand"),

                // The absolute path will be used as is.
                // FileConfiguration = new LocalFileConfiguration("C:\\Local/PathExample/FirstData.tinyhand"),

                // When specifying GlobalFileConfiguration, the path will be combined with GlobalMain of CrystalizerOptions to create an absolute path.
                // FileConfiguration = new GlobalFileConfiguration("Global/FirstData.tinyhand"),

                // You can also save data on AWS S3. Please enter authentication information using IStorageKey.
                // FileConfiguration = new S3FileConfiguration(BucketName, "Test/FirstData.tinyhand"),
            });

        if (AccessKeyPair.TryParse(KeyPair, out var accessKeyPair))
        {// AccessKeyId=SecretAccessKey
            unit.Context.ServiceProvider.GetRequiredService<IStorageKey>().AddKey(BucketName, accessKeyPair);
        }

        // await crystalizer.PrepareAndLoadAll(false); // Prepare resources for storage operations and read data from files.
        await crystal.PrepareAndLoad(false); // You can also prepare and load data individually through the ICrystal interface.
        var data = crystal.Data;

        // Unit root directory
        var unitOptions = unit.Context.ServiceProvider.GetRequiredService<UnitOptions>();
        Console.WriteLine($"UnitOptions root directory: {unitOptions.DataDirectory}");

        // Crystalizer root directory
        Console.WriteLine($"Crystalizer root directory: {crystalizer.RootDirectory}");

        Console.WriteLine($"Load {data.ToString()}"); // Id: 0 Name: Hoge
        data.Id = 1;
        data.Name = "Fuga";
        Console.WriteLine($"Save {data.ToString()}"); // Id: 1 Name: Fuga

        await crystalizer.SaveAll(); // Save all data.

        return unit;
    }
}
