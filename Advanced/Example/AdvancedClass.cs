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

    [TinyhandObject(Structual = true)]
    [ValueLinkObject(Isolation = IsolationLevel.ReadCommitted)]
    public partial class Point : StoragePoint<AdvancedExample>
    {
        public Point(int id, string name)
        {
            this.Id = id;
            this.Name = name;
        }

        /*[Key(1, AddProperty = "Id", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
        [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
        private int id;

        [Key(2, AddProperty = "Name")]
        [Link(Type = ChainType.Ordered)]
        private string name = string.Empty;*/

        [Key(1)]
        [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
        public int Id { get; private set; }

        [Key(2)]
        [Link(Type = ChainType.Ordered)]
        public string Name { get; private set; } = string.Empty;
    }

    public AdvancedExample()
    {
    }

    public async Task TestCode()
    {
        Point.GoshujinClass g = default!;
        var obj = g.Find(1, AcquisitionMode.GetOrCreate);
        // obj.Name = "Name1";
        using (g.LockObject.EnterScope())
        {
            if (g.IdChain.FindFirst(1) is null)
            {
                var newPoint = new Point(1, "Name1");
                newPoint.Goshujin = g;
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

    // [Key(0, AddProperty = "Child", PropertyAccessibility = PropertyAccessibility.GetterOnly)]
    [Key(0)]
    public StoragePoint<AdvancedExample> ChildStorage { get; private set; } = new();

    [Key(1)]
    public StoragePoint<Point.GoshujinClass> ChildrenStorage { get; private set; } = new();

    [Key(2)]
    public partial StoragePoint<byte[]> ByteArrayStorage { get; private set; } = new();
}
