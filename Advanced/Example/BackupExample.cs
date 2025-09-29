// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.ComponentModel;

namespace QuickStart;

[TinyhandObject(Structural = true)]
public partial class BackupData
{
    [Key(0, AddProperty = "Id")]
    private int id;

    [Key(1, AddProperty = "Name")]
    [DefaultValue("Back")]
    private string name = string.Empty;

    public override string ToString()
        => $"Id: {this.id}, Name: {this.name}";
}

public partial class Program
{
    public static async Task<UnitProduct> BackupExample()
    {
        // Create a builder to organize dependencies and register data configurations.
        var builder = new CrystalUnit.Builder()
            .ConfigureCrystal(context =>
            {
                context.SetJournal(new SimpleJournalConfiguration(new LocalDirectoryConfiguration("Local/BackupExample/Journal")));

                context.AddCrystal<FirstData>(
                    new()
                    {
                        SaveFormat = SaveFormat.Utf8,
                        NumberOfFileHistories = 3,
                        FileConfiguration = new LocalFileConfiguration("Local/BackupExample/FirstData.tinyhand"),
                    });

                context.AddCrystal<BackupData>(
                    new()
                    {
                        SaveFormat = SaveFormat.Utf8,
                        NumberOfFileHistories = 3,
                        FileConfiguration = new LocalFileConfiguration("Local/BackupExample/BackupData.tinyhand"),

                        // Specify the location to save the backup files individually.
                        BackupFileConfiguration = new LocalFileConfiguration("Local/BackupExample/Backup/BackupData.tinyhand"),
                    });
            })
            .PostConfigure(context =>
            {
                context.SetOptions(context.GetOptions<CrystalOptions>() with
                {
                    // When you set DefaultBackup, the backup for all data (for which BackupFileConfiguration has not been specified individually) will be saved in the directory.
                    DefaultBackup = new LocalDirectoryConfiguration(Path.Combine(context.DataDirectory, "DefaultBackup")),
                });
            });

        var product = builder.Build(); // Build.
        var crystalControl = product.Context.ServiceProvider.GetRequiredService<CrystalControl>(); // Obtains a CrystalControl instance for data storage operations.
        await crystalControl.PrepareAndLoad(false); // Prepare resources for storage operations and read data from files.

        var data = product.Context.ServiceProvider.GetRequiredService<FirstData>();
        Console.WriteLine($"First data:");
        Console.WriteLine($"Load {data.ToString()}");
        data.Id++;
        data.Name += "Fuga";
        Console.WriteLine($"Save {data.ToString()}");

        var data2 = product.Context.ServiceProvider.GetRequiredService<BackupData>();
        Console.WriteLine($"Backup data:");
        Console.WriteLine($"Load {data2.ToString()}");
        data2.Id += 2;
        data2.Name += "Up";
        Console.WriteLine($"Save {data2.ToString()}");

        await crystalControl.Store(); // Save all data.

        return product;
    }
}
