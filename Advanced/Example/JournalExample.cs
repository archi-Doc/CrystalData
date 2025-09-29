// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData;
using ValueLink;

namespace QuickStart;

[TinyhandObject(Structual = true)] // Enable the journaling feature.
[ValueLinkObject] // You can use ValuLink to handle a collection of objects.
public partial class JournalData
{
    [Key(0, AddProperty = "Id")] // Additional property is required.
    [Link(Primary = true, Unique = true, Type = ChainType.Unordered)]
    private int id;

    [Key(1, AddProperty = "Name")]
    private string name = string.Empty;

    [Key(2, AddProperty = "Count")]
    private int count;

    public JournalData()
    {
    }

    public JournalData(int id, string name)
    {
        this.id = id;
        this.name = name;
    }

    public override string ToString()
        => $"Id: {this.id}, Name: {this.name}, Count: {this.count}";
}

[TinyhandObject(Structual = true)] // Enable the journaling feature.
public partial class JournalData2
{
    [Key(0)]
    public partial int Id { get; set; }
}

public partial class Program
{
    public static async Task<UnitProduct?> JournalExample()
    {
        // Create a builder to organize dependencies and register data configurations.
        var builder = new CrystalUnit.Builder()
            .ConfigureCrystal(context =>
            {
                // Register SimpleJournal configuration.
                context.SetJournal(new SimpleJournalConfiguration(new LocalDirectoryConfiguration("Local/JournalExample/Journal"), 256));

                // Register SimpleData configuration.
                context.AddCrystal<JournalData.GoshujinClass>(
                    new CrystalConfiguration()
                    {
                        SaveFormat = SaveFormat.Utf8,
                        NumberOfFileHistories = 3, // The journaling feature is integrated with file history (snapshots), so please set it to 1 or more.
                        FileConfiguration = new LocalFileConfiguration("Local/JournalExample/JournalData.tinyhand"), // Specify the file name to save.
                    });

                context.AddCrystal<JournalData2>(
                    new CrystalConfiguration()
                    {
                        SaveFormat = SaveFormat.Utf8,
                        NumberOfFileHistories = 0,
                        FileConfiguration = new LocalFileConfiguration("Local/JournalExample/JournalData2.tinyhand"), // Specify the file name to save.
                    });
            });

        var product = builder.Build(); // Build.
        var serviceProvider = product.Context.ServiceProvider;
        var crystalControl = serviceProvider.GetRequiredService<CrystalControl>(); // Obtains a CrystalControl instance for data storage operations.
        await crystalControl.PrepareAndLoad(false); // Prepare resources for storage operations and read data from files.

        var goshujin = serviceProvider.GetRequiredService<JournalData.GoshujinClass>(); // Retrieve a data instance from the service provider.

        Console.WriteLine("Journal example:");

        var journalData2 = serviceProvider.GetRequiredService<JournalData2>();
        journalData2.Id++;
        Console.WriteLine($"JournalData2: {journalData2.Id}");

        var max = 0;
        foreach (var x in goshujin)
        {
            Console.WriteLine(x.ToString());
            x.Count++;

            max = max > x.Id ? max : x.Id;
        }

        max++;
        var data = new JournalData(max, max.ToString());
        goshujin.Add(data);

        Console.WriteLine();
        Console.WriteLine("Waiting for the journal writing process to complete...");
        await Task.Delay(SimpleJournalConfiguration.DefaultSaveIntervalInMilliseconds + 500);
        Console.WriteLine("Done.");

        return null; // Exit without saving data.
    }
}
