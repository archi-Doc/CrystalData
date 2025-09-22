// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.ComponentModel;

namespace QuickStart;

[TinyhandObject(LockObject = "syncObject")]
public partial class SecondData
{
    [Key("Id", AddProperty = "Id")] // String key "Id"
    private int id;

    [Key("Name", AddProperty = "Name")] // String key "Name"
    [DefaultValue("Hoge")] // The default value for the name property.
    private string name = string.Empty;

    private object syncObject = new(); // Object for exclusive locking.

    public override string ToString()

        => $"Id: {this.Id}, Name: {this.Name}";
}

public class SecondExample
{
    public SecondExample(CrystalControl crystalControl, ICrystal<SecondData> crystal)
    {
        this.crystalControl = crystalControl;
        this.crystal = crystal; // Get an ICrystal interface for data storage operations.
    }

    public async Task Process()
    {
        var data = this.crystal.Data; // Get a data instance via ICrystal interface.

        Console.WriteLine($"Load {data.ToString()}"); // Id: 0 Name: Hoge
        data.Id++;
        data.Name = "Fuga";
        Console.WriteLine($"Save {data.ToString()}"); // Id: 1 Name: Fuga

        await this.crystal.Store(); // Save data.

        var firstCrystal = this.crystalControl.CreateCrystal<FirstData>(new(SaveFormat.Utf8, new LocalFileConfiguration("Local/SecondExample/FirstData.tinyhand")));
        firstCrystal.Data.Id++;
        firstCrystal.Data.Name += "Nupo";

        // await firstCrystal.Save(); // The data will be automatically saved when the application is closed.
    }

    private CrystalControl crystalControl;
    private ICrystal<SecondData> crystal;
}

public partial class Program
{
    public static async Task<UnitProduct?> SecondExample()
    {
        var myBuilder = new UnitBuilder();

        var crystalDataBuilder = new CrystalUnit.Builder()
            .Configure(context =>
            {
                context.TryAddSingleton<SecondExample>(); // Register SecondExample class.

                context.AddLoggerResolver(context =>
                {// Add logger resolver
                    if (context.LogLevel == LogLevel.Debug)
                    {
                        context.SetOutput<ConsoleAndFileLogger>();
                        return;
                    }

                    context.SetOutput<ConsoleLogger>();
                });
            })
            .ConfigureCrystal(context =>
            {
                context.AddCrystal<SecondData>(
                    new CrystalConfiguration()
                    {
                        SaveFormat = SaveFormat.Utf8, // Format is utf8 text.
                        NumberOfFileHistories = 2, // 2 history files.
                        FileConfiguration = new LocalFileConfiguration("Local/SecondExample/SecondData.tinyhand"), // Specify the file name to save.
                        BackupFileConfiguration = new LocalFileConfiguration("Backup/SecondExample/SecondData.tinyhand"), // The backup file name.
                        RequiredForLoading = true,
                    });
            })
            .PostConfigure(context =>
            {
                context.SetOptions(context.GetOptions<CrystalOptions>() with
                {
                    EnableFilerLogger = true, // Enable filer logger.
                    DataDirectory = Directory.GetCurrentDirectory(),
                });

                var logfile = "Logs/Log.txt";
                context.SetOptions(context.GetOptions<FileLoggerOptions>() with
                {
                    Path = Path.Combine(context.DataDirectory, logfile),
                    MaxLogCapacity = 2,
                });
            });

        myBuilder.AddBuilder(crystalDataBuilder);

        var product = myBuilder.Build(); // Build.
        var crystalControl = product.Context.ServiceProvider.GetRequiredService<CrystalControl>();
        var result = await crystalControl.PrepareAndLoad(true); // Use the default query.
        if (result.IsFailure())
        {// Abort
            return default;
        }

        var example = product.Context.ServiceProvider.GetRequiredService<SecondExample>();
        await example.Process();

        return product;
    }
}
