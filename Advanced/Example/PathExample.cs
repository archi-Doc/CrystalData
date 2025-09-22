// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace QuickStart;

public partial class Program
{
    private const string BucketName = "test_bucket";
    private const string KeyPair = "AccessKeyId=SecretAccessKey";

    public static async Task<BuiltUnit> PathExample()
    {
        // Create a builder to organize dependencies and register data configurations.
        var builder = new CrystalUnit.Builder()
            .ConfigureCrystal(context =>
            {
            })
            .PostConfigure(context =>
            {
                context.SetOptions(context.GetOptions<CrystalOptions>() with
                {
                    // RootPath = Path.Combine(context.RootDirectory, "Additional"), // Root directory
                    GlobalDirectory = new LocalDirectoryConfiguration(Path.Combine(context.DataDirectory, "Global")), // Global directory
                });
            });

        var product = builder.Build(); // Build.
        var crystalControl = product.Context.ServiceProvider.GetRequiredService<CrystalControl>(); // Obtains a CrystalControl instance for data storage operations.

        // Get the crystal from crystalControl.
        var crystal = crystalControl.GetOrCreateCrystal<FirstData>(
            new CrystalConfiguration()
            {
                SaveFormat = SaveFormat.Utf8, // Format is utf8 text.

                // If a relative path is specified, it combines the root directory of CrystalControl with the path to create an absolute path.
                FileConfiguration = new LocalFileConfiguration("Local/PathExample/FirstData.tinyhand"),

                // The absolute path will be used as is.
                // FileConfiguration = new LocalFileConfiguration("C:\\Local/PathExample/FirstData.tinyhand"),

                // When specifying GlobalFileConfiguration, the path will be combined with GlobalMain of CrystalOptions to create an absolute path.
                // FileConfiguration = new GlobalFileConfiguration("Global/FirstData.tinyhand"),

                // You can also save data on AWS S3. Please enter authentication information using IStorageKey.
                // FileConfiguration = new S3FileConfiguration(BucketName, "Test/FirstData.tinyhand"),
            });

        if (AccessKeyPair.TryParse(KeyPair, out var accessKeyPair))
        {// AccessKeyId=SecretAccessKey
            product.Context.ServiceProvider.GetRequiredService<IStorageKey>().AddKey(BucketName, accessKeyPair);
        }

        // await crystalControl.PrepareAndLoadAll(false); // Prepare resources for storage operations and read data from files.
        await crystal.PrepareAndLoad(false); // You can also prepare and load data individually through the ICrystal interface.
        var data = crystal.Data;

        // Unit root directory
        var unitOptions = product.Context.ServiceProvider.GetRequiredService<UnitOptions>();
        Console.WriteLine($"UnitOptions root directory: {unitOptions.DataDirectory}");

        // CrystalControl root directory
        Console.WriteLine($"CrystalControl root directory: {crystalControl.Options.DataDirectory}");

        Console.WriteLine($"Load {data.ToString()}"); // Id: 0 Name: Hoge
        data.Id = 1;
        data.Name = "Fuga";
        Console.WriteLine($"Save {data.ToString()}"); // Id: 1 Name: Fuga

        await crystalControl.Store(); // Save all data.

        return product;
    }
}
