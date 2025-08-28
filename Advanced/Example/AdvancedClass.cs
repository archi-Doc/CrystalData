// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

// From a quite simple class for data storage...
[TinyhandObject]
public partial record SimpleExample
{
    public SimpleExample()
    {
    }

    [Key(0)]
    public string UserName { get; set; } = string.Empty;
}

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.ReadCommitted)]
public partial class Point2 : StoragePoint<AdvancedExample>
{
    [Key(1, AddProperty = "Id", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    private int id;

    [Key(2, AddProperty = "Name")]
    [Link(Type = ChainType.Ordered)]
    private string name = string.Empty;
}

// To a complex class designed for handling large-scale data in terms of both quantity and capacity.
[TinyhandObject(Structual = true)]
public partial record AdvancedExample
{// This is it. This class is the crystal of the most advanced data management architecture I've reached so far.
    public static void Register(ICrystalUnitContext context)
    {
        context.AddCrystal<AdvancedExample>(
            new()
            {
                SaveFormat = SaveFormat.Binary,
                SavePolicy = SavePolicy.Periodic,
                SaveInterval = TimeSpan.FromMinutes(10),
                FileConfiguration = new GlobalFileConfiguration("AdvancedExampleMain.tinyhand"),
                BackupFileConfiguration = new GlobalFileConfiguration("AdvancedExampleBackup.tinyhand"),
                StorageConfiguration = new SimpleStorageConfiguration(
                    new GlobalDirectoryConfiguration("MainStorage"),
                    new GlobalDirectoryConfiguration("BackupStorage")),
                NumberOfFileHistories = 2,
            });

        context.TrySetJournal(new SimpleJournalConfiguration(new S3DirectoryConfiguration("TestBucket", "Journal")));
    }

    public AdvancedExample()
    {
    }

    [Key(0, AddProperty = "Child", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    private StoragePoint<AdvancedExample> child = new();

    [Key(1, AddProperty = "Children", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    private StoragePoint<Point2.GoshujinClass> children = new();

    [Key(2, AddProperty = "ByteArray", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    private StoragePoint<byte[]> byteArray = new();
}
