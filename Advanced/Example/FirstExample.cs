// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.ComponentModel;

namespace QuickStart;

[TinyhandObject] // Annotate TinyhandObject attribute to make this class serializable.
public partial class FirstData
{
    [Key(0)] // The key attribute specifies the index at serialization
    public int Id { get; set; }

    [Key(1)]
    [DefaultValue("Hoge")] // The default value for the name property.
    public string Name { get; set; } = string.Empty;

    public override string ToString()
        => $"Id: {this.Id}, Name: {this.Name}";
}

public partial class Program
{
    public static async Task<BuiltUnit> FirstExample()
    {
        // Create a builder to organize dependencies and register data configurations.
        var builder = new CrystalUnit.Builder()
            .ConfigureCrystal(context =>
            {
                // Register SimpleData configuration.
                context.AddCrystal<FirstData>(
                    new CrystalConfiguration()
                    {
                        SaveFormat = SaveFormat.Utf8, // Format is utf8 text.
                        NumberOfFileHistories = 0, // No history file.
                        FileConfiguration = new LocalFileConfiguration("Local/FirstExample/FirstData.tinyhand"), // Specify the file name to save.
                    });
            });

        var unit = builder.Build(); // Build.
        var crystalControl = unit.Context.ServiceProvider.GetRequiredService<CrystalControl>(); // Obtains a CrystalControl instance for data storage operations.
        await crystalControl.PrepareAndLoad(false); // Prepare resources for storage operations and read data from files.

        var data = unit.Context.ServiceProvider.GetRequiredService<FirstData>(); // Retrieve a data instance from the service provider.

        Console.WriteLine($"Load {data.ToString()}"); // Id: 0 Name: Hoge
        data.Id = 1;
        data.Name = "Fuga";
        Console.WriteLine($"Save {data.ToString()}"); // Id: 1 Name: Fuga

        await crystalControl.Store(); // Save all data.

        return unit;
    }
}
