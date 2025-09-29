## CrystalData is a storage engine for C#
![Nuget](https://img.shields.io/nuget/v/CrystalData) ![Build and Test](https://github.com/archi-Doc/CrystalData/workflows/Build%20and%20Test/badge.svg)

- Very versatile and easy to use.
- Covers a wide range of storage needs.

- Full serialization features integrated with [Tinyhand](https://github.com/archi-Doc/Tinyhand) and [ValueLink](https://github.com/archi-Doc/ValueLink).



## Table of Contents

- [Requirements](#requirements)
- [Quick Start](#quick-start)
- [Configuration](#configuration)



## Requirements

**Visual Studio 2022** or later.



## Quick start

Install **CrystalData** using Package Manager Console.

```
Install-Package CrystalData
```

This is a small example code to use **CrystalData**.

```csharp
// First, create a class to represent the data content.
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
```

```csharp
// Create a builder to organize dependencies and register data configurations.
var builder = new CrystalUnit.Builder()
    .ConfigureCrystal(context =>
    {
        // Register FirstData configuration.
        context.AddCrystal<FirstData>(
            new CrystalConfiguration()
            {
                SaveFormat = SaveFormat.Utf8, // The format is utf8 text.
                NumberOfFileHistories = 0, // No history file.
                FileConfiguration = new LocalFileConfiguration("Local/SimpleExample/SimpleData.tinyhand"), // Specify the file name to save.
            });
    });

var product = builder.Build(); // Build.
var crystalControl = product.Context.ServiceProvider.GetRequiredService<CrystalControl>(); // Obtains a CrystalControl instance for data storage operations.
await crystalControl.PrepareAndLoad(false); // Prepare resources for storage operations and read data from files.

var data = product.Context.ServiceProvider.GetRequiredData<FirstData>(); // Retrieve a data instance from the service provider.

Console.WriteLine($"Load {data.ToString()}"); // Id: 0 Name: Hoge
data.Id += 1;
data.Name += "Fuga";
Console.WriteLine($"Save {data.ToString()}"); // Id: 1 Name: Fuga

await crystalControl.StoreAndRip(); // Save data and perform the shutdown process.
```



## Advanced

CrystalData is designed to cover a really wide range of storage needs.

```csharp
// From a quite simple class for data storage...
[TinyhandObject]
public partial record SimpleClass
{
    public SimpleClass()
    {
    }

    [Key(0)]
    public string UserName { get; set; } = string.Empty;
}

// To a complex class designed for handling large-scale data in terms of both quantity and capacity.
[TinyhandObject(Structual = true)]
public partial record AdvancedClass
{// This is it. This class is the crystal of the most advanced data management architecture I've reached so far.
    public static void Register(ICrystalConfigurationContext context)
    {
        context.AddCrystal<AdvancedClass>(
            new()
            {
                SaveFormat = SaveFormat.Binary,
                SaveInterval = TimeSpan.FromMinutes(10),
                FileConfiguration = new GlobalFileConfiguration("AdvancedExampleMain"),
                BackupFileConfiguration = new GlobalFileConfiguration("AdvancedExampleBackup"),
                StorageConfiguration = new SimpleStorageConfiguration(
                    new GlobalDirectoryConfiguration("MainStorage"),
                    new GlobalDirectoryConfiguration("BackupStorage")),
                NumberOfFileHistories = 2,
            });

        context.TrySetJournal(new SimpleJournalConfiguration(new S3DirectoryConfiguration("TestBucket", "Journal")));
    }

    [TinyhandObject(Structual = true)]
    [ValueLinkObject(Isolation = IsolationLevel.ReadCommitted)]
    public partial class Point : StoragePoint<AdvancedClass>
    {
        public void TryInitialize(int id)
        {
            if (this.Id == 0)
            {
                this.Id = id;
            }
        }

        [Key(1)]
        [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
        public int Id { get; private set; }
    }

    public AdvancedClass()
    {
    }

    [Key(0)]
    public int Id { get; private set; }

    [Key(1)]
    public partial string Name { get; set; } = "Test";

    [Key(2)]
    public StoragePoint<AdvancedClass> ChildStorage { get; private set; } = new();

    [Key(3)]
    public StoragePoint<Point.GoshujinClass> ChildrenStorage { get; private set; } = new();

    [Key(4)]
    public partial StoragePoint<byte[]> ByteArrayStorage { get; private set; } = new();
}
```



## Configuration

By assigning a **CrystalConfiguration** to the data class, you can specify the timing, format of data save, the number of history files, and the file path.

```csharp
context.AddCrystal<FirstData>(
    new CrystalConfiguration()
    {
        SavePolicy = SavePolicy.Manual, // Timing of saving data is controlled by the application.
        SaveFormat = SaveFormat.Utf8, // Format is utf8 text.
        NumberOfHistoryFiles = 0, // No history file.
        FileConfiguration = new LocalFileConfiguration("Local/FirstExample/FirstData.tinyhand"), // Specify the file name to save.
    });
```




### Timing of data persistence

Data persistence is a core feature of CrystalData and its timing is critical. There are several options for when to save data.
The following code is for preparation.

```csharp
[TinyhandObject(Journaling = true)] // Journaling feature is necessary to allow the function to save data when properties are changed.
public partial class SaveTimingData
{
    [Key(0, AddProperty = "Id")] // Add a property to save data when the value is changed.
    internal int id;

    public override string ToString()
        => $"Id: {this.Id}";
}
```

```csharp
var crystal = unit.Context.ServiceProvider.GetRequiredService<ICrystal<SaveTimingData>>();
var data = crystal.Data;
```



#### Save manually

Save the data manually after it has been changed, and wait until the save process is complete.

```csharp
context.AddCrystal<SaveTimingData>(
    new CrystalConfiguration()
    {
        SavePolicy = SavePolicy.Manual, // Timing of saving data is controlled by the application.
        FileConfiguration = new LocalFileConfiguration("Local/SaveTimingExample/SaveTimingData.tinyhand"), // Specify the file name to save.
    });
```

```csharp
// Save manually
data.id += 1;
await crystal.Save();
```



#### On changed

When data is changed, it is registered in the save queue and will be saved in a second.

```csharp
context.AddCrystal<SaveTimingData>(
    new CrystalConfiguration()
    {
        SavePolicy = SavePolicy.OnChanged,
        FileConfiguration = new LocalFileConfiguration("Local/SaveTimingExample/SaveTimingData.tinyhand"), // Specify the file name to save.
    });
```

```csharp
// Add to the save queue when the value is changed
data.Id += 2;

// Alternative
data.id += 2;
crystal.TryAddToSaveQueue();
```



#### Periodic

By setting **SavePolicy** to **Periodic** in **CrystalConfiguration**, data can be saved at regular intervals.

```csharp
context.AddCrystal<SaveTimingData>(
    new CrystalConfiguration()
    {
        SavePolicy = SavePolicy.Periodic, // Data will be saved at regular intervals.
        SaveInterval = TimeSpan.FromMinutes(1), // The interval at which data is saved.
        SaveFormat = SaveFormat.Utf8, // Format is utf8 text.
        NumberOfHistoryFiles = 0, // No history file.
        FileConfiguration = new LocalFileConfiguration("Local/SaveTimingExample/SaveTimingData.tinyhand"), // Specify the file name to save.
    });
```




#### When exiting the application

Add the following code to save all data and release resources when the application exits.

```csharp
await unit.Context.ServiceProvider.GetRequiredService<CrystalControl>().SaveAllAndTerminate();
```



#### Volatile

Data is volatile and not saved.

```cahrp
context.AddCrystal<SaveTimingData>(
    new CrystalConfiguration()
    {
        SavePolicy = SavePolicy.Volatile,
        FileConfiguration = new LocalFileConfiguration("Local/SaveTimingExample/SaveTimingData.tinyhand"), // Specify the file name to save.
    });
```




### Timing of configuration and instantiation

#### Builder pattern

Create a **CrystalControl.Builder** and register Data using the **ConfigureCrystal()** and **AddCrystal()** methods. As Data is registered in the DI container, it can be easily used.

```csharp
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
                NumberOfHistoryFiles = 0, // No history file.
                FileConfiguration = new LocalFileConfiguration("Local/FirstExample/FirstData.tinyhand"), // Specify the file name to save.
            });
    });

var unit = builder.Build(); // Build.
```

```csharp
public class ConfigurationExampleClass
{
    public ConfigurationExampleClass(CrystalControl crystalControl, FirstData firstData)
    {
        this.crystalControl = crystalControl;
        this.firstData = firstData;
    }
}
```



#### CrystalControl

Create an **ICrystal** object using the **CrystalControl**.

```csharp
// Get or create an ICrystal interface of the data.
var crystal = this.crystalControl.GetOrCreateCrystal<SecondData>(
    new CrystalConfiguration(
        SavePolicy.Manual,
        new LocalFileConfiguration("Local/ConfigurationTimingExample/SecondData.tinyhand")));
var secondData = crystal.Data;

// You can create multiple crystals from single data class.
var crystal2 = this.crystalControl.CreateCrystal<SecondData>(
    new CrystalConfiguration(
        SavePolicy.Manual,
        new LocalFileConfiguration("Local/ConfigurationTimingExample/SecondData2.tinyhand")));
var secondData2 = crystal2.Data;
```



### Specifying the path

You can set the path to save the data by specifying the **FileConfiguration** of **CrystalConfiguration**.

The path can be a basic local absolute path, a relative path, or an AWS S3 path.

```csharp
context.AddCrystal<FirstData>(
    new CrystalConfiguration()
    {
        FileConfiguration = new LocalFileConfiguration("Local/FirstExample/FirstData.tinyhand"), // Specify the file name to save.
    });
```



#### Local path

If a relative path is specified, it combines the root directory of **CrystalControl** with the path to create an absolute path.

```csharp
FileConfiguration = new LocalFileConfiguration("Local/PathExample/FirstData.tinyhand"),
```

The absolute path will be used as is.

```csharp
FileConfiguration = new LocalFileConfiguration("C:\\Local/PathExample/FirstData.tinyhand"),
```



#### Global path

When specifying **GlobalFileConfiguration**, the path will be combined with **GlobalDirectory** of **CrystalOptions** to create an absolute path.

```csharp
FileConfiguration = new GlobalFileConfiguration("Global/FirstData.tinyhand"),
```

```csharp
var builder = new CrystalControl.Builder()
    .ConfigureCrystal(context =>
    {
    })
    .SetupOptions<CrystalOptions>((context, options) =>
    {// You can change the root directory of the CrystalData by modifying CrystalOptions.
        context.GetOptions<UnitOptions>(out var unitOptions);// Get the application root directory.
        if (unitOptions is not null)
        {
            // options.RootPath = Path.Combine(unitOptions.RootDirectory, "Additional"); // Root directory
            options.GlobalDirectory = new LocalDirectoryConfiguration(Path.Combine(unitOptions.RootDirectory, "Global")); // Global directory
        }
    });
```



#### AWS S3

You can also save data on AWS S3. Please enter authentication information using **IStorageKey**.

```csharp
FileConfiguration = new S3FileConfiguration(BucketName, "Test/FirstData.tinyhand"),
```

```csharp
if (AccessKeyPair.TryParse(KeyPair, out var accessKeyPair))
{// AccessKeyId=SecretAccessKey
    unit.Context.ServiceProvider.GetRequiredService<IStorageKey>().AddKey(BucketName, accessKeyPair);
}
```



### Backup

By setting up a backup configuration, you can recover data from the backup file even if the main file is lost.

```csharp
context.AddCrystal<BackupData>(
    new()
    {
        SaveFormat = SaveFormat.Utf8, // Format is utf8 text.
        NumberOfFileHistories = 3,
        FileConfiguration = new LocalFileConfiguration("Local/BackupExample/BackupData.tinyhand"),

        // Specify the location to save the backup files individually.
        BackupFileConfiguration = new LocalFileConfiguration("Local/BackupExample/Backup/BackupData.tinyhand"),
    });
```

```csharp
.SetupOptions<CrystalOptions>((context, options) =>
{
    context.GetOptions<UnitOptions>(out var unitOptions);// Get the application root directory.
    if (unitOptions is not null)
    {
        // When you set DefaultBackup, the backup for all data (for which BackupFileConfiguration has not been specified individually) will be saved in the directory.
        options.DefaultBackup = new LocalDirectoryConfiguration(Path.Combine(unitOptions.RootDirectory, "DefaultBackup"));
    }
});
```



The process of loading data is as follows:

1. Load the main file.
2. If it fails, load the backup file.
3. If there are history files (main or backup), load the latest history file.
4. When journaling is enabled (detailed later), load the Journal to update to the most recent data.

By performing the above processes, **CrystalData** tries to minimize data loss.



## Journaling

**CrystalData** offers a limited journaling feature to enhance data durability.

The goal is to minimize data loss in the event of a failure, reducing potential loss from one hour to one second.

Here is an example class.

```csharp
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
```

To use the journal feature, please set **NumberOfFileHistories** to greater than or equal to 1 in **CrystalConfiguration** and configure the journal with `context.SetJournal()`.

```csharp
var builder = new CrystalControl.Builder()
    .ConfigureCrystal(context =>
    {
        // Register SimpleData configuration.
        context.AddCrystal<JournalData.GoshujinClass>(
            new CrystalConfiguration()
            {
                SavePolicy = SavePolicy.Manual, // Timing of saving data is controlled by the application.
                SaveFormat = SaveFormat.Utf8, // Format is utf8 text.
                NumberOfFileHistories = 1, // The journaling feature is integrated with file history (snapshots), so please set it to 1 or more.
                FileConfiguration = new LocalFileConfiguration("Local/JournalExample/JournalData.tinyhand"), // Specify the file name to save.
            });

        context.SetJournal(new SimpleJournalConfiguration(new LocalDirectoryConfiguration("Local/JournalExample/Journal")));
    });
```



## StoragePoint



| **Class**                                | Persistence | Element  | Data control | Exclusive control |
| ---------------------------------------- | ----------- | -------- | ------------ | ----------------- |
| **SptClass.GoshujinClass**               | Parent      | SptClass | Parent       | Goshujin          |
| **StoragePoint<SptClass.GoshujinClass>** | Storage     | SptClass | StoragePoint | Goshujin          |
| **SptPoint.GoshujinClass**               | Parent      | SptPoint | Parent       | SptPoint          |
| **StoragePoint<SptPoint.GoshujinClass>** | Storage     | SptPoint | StoragePoint | SptPoint          |

