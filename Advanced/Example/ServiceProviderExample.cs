// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace QuickStart;

[TinyhandObject(UseServiceProvider = true)] // Annotate TinyhandObject attribute to make this class serializable.
public partial class ServiceProviderData
{
    public ServiceProviderData(FirstData firstData)
    {
        this.FirstData = firstData;
    }

    [Key(0)]
    public double Age { get; set; }

    [Key(1)]
    public FirstData FirstData { get; set; }

    public override string ToString()
        => $"{this.FirstData.ToString()} Age: {this.Age.ToString()}";
}

public partial class Program
{
    public static async Task<UnitProduct> ServiceProviderExample()
    {
        // Create a builder to organize dependencies and register data configurations.
        var builder = new CrystalUnit.Builder()
            .Configure(context =>
            {
                context.AddSingleton<FirstData>();
                context.AddSingleton<ServiceProviderData>();
            })
            .ConfigureCrystal(context =>
            {
                // Register SimpleData configuration.
                context.AddCrystal<ServiceProviderData>(
                    new CrystalConfiguration()
                    {
                        SaveFormat = SaveFormat.Utf8, // Format is utf8 text.
                        NumberOfFileHistories = 0, // No history file.
                        FileConfiguration = new LocalFileConfiguration("Local/ServiceProviderExample/ServiceProviderExample.tinyhand"), // Specify the file name to save.
                    });
            });

        var product = builder.Build(); // Build.
        TinyhandSerializer.ServiceProvider = product.Context.ServiceProvider;
        var crystalControl = product.Context.ServiceProvider.GetRequiredService<CrystalControl>(); // Obtains a CrystalControl instance for data storage operations.
        await crystalControl.PrepareAndLoad(false); // Prepare resources for storage operations and read data from files.

        // var data = product.Context.ServiceProvider.GetRequiredService<ICrystal<ServiceProviderData>>().Data; // Retrieve a data instance from the service provider.
        var data = product.Context.ServiceProvider.GetRequiredService<ServiceProviderData>(); // Retrieve a data instance from the service provider.

        Console.WriteLine($"Load {data.ToString()}");
        data.FirstData.Id++;
        data.Age += 1000d;
        Console.WriteLine($"Save {data.ToString()}");

        await crystalControl.Store(); // Save all data.

        return product;
    }
}
