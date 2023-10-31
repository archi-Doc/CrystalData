// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using ValueLink;

namespace QuickStart;

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.RepeatableRead)]
public partial record IntegratedData
{
    [Key(0, AddProperty = "Id")]
    [Link(Primary = true, Unique = true, Type = ChainType.Unordered)]
    private int id;

    [Key(1, AddProperty = "Name")]
    private string name = string.Empty;

    [Key(2, AddProperty = "Count")]
    private int count;

    public IntegratedData()
    {
        // Tinyhand: Object serialization.
        // ValueLink: Object collection.
        // CrystalData: Object persistence.
    }

    public IntegratedData(int id, string name)
    {
        this.id = id;
        this.name = name;
    }

    public override string ToString()
        => $"Id: {this.id}, Name: {this.name}, Count: {this.count}";
}

public partial class Program
{
    public static async Task<BuiltUnit?> IntegratedExample()
    {
        // Create a builder to organize dependencies and register data configurations.
        var builder = new CrystalControl.Builder()
            .ConfigureCrystal(context =>
            {
                // Register SimpleData configuration.
                context.AddCrystal<IntegratedData.GoshujinClass>(
                    new CrystalConfiguration()
                    {
                        SavePolicy = SavePolicy.Manual, // Timing of saving data is controlled by the application.
                        SaveFormat = SaveFormat.Utf8, // Format is utf8 text.
                        NumberOfFileHistories = 1, // The journaling feature is integrated with file history (snapshots), so please set it to 1 or more.
                        FileConfiguration = new LocalFileConfiguration("Local/IntegratedExample/IntegratedData.tinyhand"), // Specify the file name to save.
                    });

                context.SetJournal(new SimpleJournalConfiguration(new LocalDirectoryConfiguration("Local/IntegratedExample/Journal")));
            });

        var unit = builder.Build(); // Build.
        var crystalizer = unit.Context.ServiceProvider.GetRequiredService<Crystalizer>(); // Obtains a Crystalizer instance for data storage operations.
        await crystalizer.PrepareAndLoadAll(false); // Prepare resources for storage operations and read data from files.

        var goshujin = unit.Context.ServiceProvider.GetRequiredService<IntegratedData.GoshujinClass>(); // Retrieve a data instance from the service provider.

        Console.WriteLine("Integrated example:");

        var array = goshujin.GetArray();
        foreach (var x in array)
        {
            Console.WriteLine(x.ToString());
        }

        using (var w = await goshujin.TryLockAsync(0, TryLockMode.GetOrCreate))
        {
            if (w is not null)
            {
                w.Count++;
                w.Commit();
            }
        }

        return unit;
    }
}
