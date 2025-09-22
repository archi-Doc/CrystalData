// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

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

    public async Task TestCode()
    {
        Point.GoshujinClass g = default!;
        var obj = g.Find(1, AcquisitionMode.GetOrCreate);
        // obj.Name = "Name1";
        using (var dataScope = await g.TryLock(1, AcquisitionMode.GetOrCreate))
        {
            if (dataScope.IsValid)
            {
            }
        }

        using (var scope = await g.TryLock(1, AcquisitionMode.GetOrCreate))
        {
            // Func<int, Point> newPoint = (int id) => new Point() { Id = id, Name = $"Name{id}" };
            if (scope.IsValid)
            {
            }
        }

        var sc = g.Find(3, AcquisitionMode.Get);
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
