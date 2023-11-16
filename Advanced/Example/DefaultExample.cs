// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.ComponentModel;

namespace QuickStart;

[TinyhandObject] // Annotate TinyhandObject attribute to make this class serializable.
public partial class DefaultData
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
    public static async Task<BuiltUnit> DefaultExample()
    {
        // Create a builder to organize dependencies and register data configurations.
        var builder = new CrystalControl.Builder()
            .ConfigureCrystal(context =>
            {
                // Register data configuration.
                context.AddCrystal<DefaultData>(new(new LocalFileConfiguration("Local/DefaultExample/DefaultData.tinyhand")));
            })
            .SetupOptions<CrystalizerOptions>((context, options) =>
            {
                options.DefaultSaveFormat = SaveFormat.Utf8;
                options.DefaultSavePolicy = SavePolicy.Periodic;
            });

        var unit = builder.Build(); // Build.
        var crystalizer = unit.Context.ServiceProvider.GetRequiredService<Crystalizer>(); // Obtains a Crystalizer instance for data storage operations.
        await crystalizer.PrepareAndLoadAll(false); // Prepare resources for storage operations and read data from files.

        var data = unit.Context.ServiceProvider.GetRequiredService<DefaultData>(); // Retrieve a data instance from the service provider.

        Console.WriteLine($"Load {data.ToString()}"); // Id: 0 Name: Hoge
        data.Id = 1;
        data.Name = "Fuga";
        Console.WriteLine($"Save {data.ToString()}"); // Id: 1 Name: Fuga

        await crystalizer.SaveAll(); // Save all data.

        return unit;
    }
}
