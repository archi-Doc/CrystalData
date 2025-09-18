// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Arc.Threading;
using Arc.Unit;
using CrystalData;
using Microsoft.Extensions.DependencyInjection;
using Tinyhand;
using ValueLink;

namespace Sandbox;

public sealed partial class CrystalSupplement
{
    [TinyhandObject(LockObject = "lockObject")]
    private sealed partial class Data
    {
        [TinyhandObject]
        [ValueLinkObject]
        private sealed partial class PlaneItem
        {
            public PlaneItem()
            {
            }

            [Key(0)]
            [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
            public uint Plane { get; private set; }
        }

        private readonly Lock lockObject = new();

        [Key(0)]
        private readonly HashSet<ulong> previouslyStoredIdentifiers = new();

        [Key(1)]
        private readonly PlaneItem.GoshujinClass planeItems = new();
    }
}

[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.Serializable)]
public partial record SpSecondClass
{
    public SpSecondClass(int id)
    {
        this.Id = id;
    }

    [Key(0)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    public int Id { get; set; }

    [Key(1)]
    public StoragePoint<FirstData> FirstDataStorage { get; set; } = new();
}

[TinyhandObject(Structual = true)]
public partial class FirstData
{// Object:16, Structual:20, Member:4+8+16
    public FirstData()
    {
    }

    [Key(0)] // The key attribute specifies the index at serialization
    public partial int Id { get; set; }

    [Key(1)]
    public string Name { get; set; } = default!; //"Hoge";

    [Key(2)]
    public StoragePoint<DoubleClass> DoubleStorage { get; set; } = new();

    public override string ToString()
        => $"Id: {this.Id}, Name: {this.Name}, Double: {this.DoubleStorage.TryGet().Result}";
}

[TinyhandObject(Structual = true)]
public partial class SizeTestClass
{// Object:16, Structual:20, Int:4, String:8, StoragePoint:8+32
    public SizeTestClass()
    {
    }

    [Key(0)]
    public int Int { get; set; }

    [Key(1)]
    public string Name { get; set; } = default!; //"Hoge";

    [Key(2)]
    public StoragePoint<DoubleClass> DoubleStorage { get; set; } = new();

    // [IgnoreMember]
    // public int Int3 { get; set; }
}

[TinyhandObject(Structual = true)]
public partial class DoubleClass
{
    [Key(0)]
    public double Double { get; set; }

    public DoubleClass()
    {
    }

    public override string ToString()
        => $"Double {this.Double}";
}

[TinyhandObject(Structual = true)]
// [TinyhandObject(Structual = true, UseServiceProvider = true)]
public partial class SecondData
{
    public SecondData()
    {
    }

    [Key(0)]
    public StoragePoint<SecondDataClass> ClassStorage { get; set; } = new();

    [Key(1)]
    public SpClassPoint.GoshujinClass SpClassGoshujin { get; set; } = new();

    [Key(2)]
    public StoragePoint<SpClassPoint.GoshujinClass> GoshujinStorage { get; set; } = new();

    public override string ToString()
        => $"Second: {this.ClassStorage.TryGet()}";
}

[TinyhandObject(Structual = true)]
public partial class SecondDataClass
{
    [Key(0)]
    public partial double Double { get; set; }

    [Key(1)]
    public partial int Int { get; set; }

    public override string ToString()
        => $"Class {this.Double} {this.Int}";
}

/// <summary>
/// A class at the isolation level of StoragePoint that inherits from <see cref="StoragePoint{TData}" />.
/// It maintains relationship information between classes and owns data of type TData as storage.
/// </summary>
[TinyhandObject(Structual = true)]
[ValueLinkObject(Isolation = IsolationLevel.ReadCommitted)]
public partial class SpClassPoint : StoragePoint<SpClass>
{// Value, Link
    [Key(1)]
    [Link(Unique = true, Primary = true, Type = ChainType.Unordered)]
    public int Id { get; set; }
}

public static class Helper
{// TData:SpClass, TObject:SpClassPoint, TGoshujin: SpClassPoint.GoshujinClass
    /*public static ValueTask<SpClassPoint?> Find(this CrystalData.StoragePoint<SpClassPoint.GoshujinClass> storagePoint, int key, AcquisitionMode acquisitionMode = AcquisitionMode.Get, CancellationToken cancellationToken = default)
        => Find(storagePoint, key, acquisitionMode, ValueLinkGlobal.LockTimeout, cancellationToken);

    public static async ValueTask<SpClassPoint?> Find(this CrystalData.StoragePoint<SpClassPoint.GoshujinClass> storagePoint, int key, AcquisitionMode acquisitionMode, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using (var scope = await storagePoint.TryLock(AcquisitionMode.Get, timeout, cancellationToken).ConfigureAwait(false))
        {
            if (scope.Data is { } g) return g.FindFirst(key, acquisitionMode);
            else return default;
        }
    }

    public static ValueTask<DataScope<SpClass>> TryLock(this CrystalData.StoragePoint<SpClassPoint.GoshujinClass> storagePoint, int key, AcquisitionMode acquisitionMode, CancellationToken cancellationToken = default)
        => TryLock(storagePoint, key, acquisitionMode, ValueLinkGlobal.LockTimeout, cancellationToken);

    public static async ValueTask<DataScope<SpClass>> TryLock(this CrystalData.StoragePoint<SpClassPoint.GoshujinClass> storagePoint, int key, AcquisitionMode acquisitionMode, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        SpClassPoint? point = default;
        using (var scope = await storagePoint.TryLock(AcquisitionMode.GetOrCreate, timeout, cancellationToken).ConfigureAwait(false))
        {
            if (scope.Data is { } g) point = g.FindFirst(key, acquisitionMode);
            else return new(scope.Result);
        }

        if (point is null) return new(DataScopeResult.NotFound);
        else return await point.TryLock(AcquisitionMode.GetOrCreate, timeout, cancellationToken).ConfigureAwait(false);
    }

    public static ValueTask<SpClass?> TryGet(this CrystalData.StoragePoint<SpClassPoint.GoshujinClass> storagePoint, int key, CancellationToken cancellationToken = default) => TryGet(storagePoint, key, ValueLinkGlobal.LockTimeout, cancellationToken);

    public static async ValueTask<SpClass?> TryGet(this CrystalData.StoragePoint<SpClassPoint.GoshujinClass> storagePoint, int key, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var g = await storagePoint.TryGet().ConfigureAwait(false);
        if (g is null) return default;
        else return await g.TryGet(key, timeout, cancellationToken).ConfigureAwait(false);
    }

    public static Task<DataScopeResult> Delete(this CrystalData.StoragePoint<SpClassPoint.GoshujinClass> storagePoint, int key, DateTime forceDeleteAfter = default)
        => Delete(storagePoint, key, ValueLinkGlobal.LockTimeout, default, forceDeleteAfter);

    public static async Task<DataScopeResult> Delete(this CrystalData.StoragePoint<SpClassPoint.GoshujinClass> storagePoint, int key, TimeSpan timeout, CancellationToken cancellationToken, DateTime forceDeleteAfter = default)
    {
        using (var scope = await storagePoint.TryLock(AcquisitionMode.Get, timeout, cancellationToken).ConfigureAwait(false))
        {
            if (scope.Data is { } g) return await g.Delete(key, forceDeleteAfter).ConfigureAwait(false);
            else return scope.Result;
        }
    }*/
}

[TinyhandObject(Structual = true)]
public partial class SpClass
{
    public SpClass()
    {
        this.Name = string.Empty;
    }

    [Key(0)]
    public partial string Name { get; set; }
}

internal class Program
{
    public static async Task Main(string[] args)
    {
        // Create a builder to organize dependencies and register data configurations.
        var builder = new CrystalControl.Builder()
            .Configure(context =>
            {
                // context.AddSingleton<FirstData>();
                context.AddSingleton<SecondData>();
            })
            .ConfigureCrystal((unitContext, crystalContext) =>
            {
                // CrystalizerOptions
                crystalContext.SetCrystalizerOptions(new CrystalizerOptions() with
                {
                    SaveDelay = TimeSpan.FromSeconds(5),
                    GlobalDirectory = new LocalDirectoryConfiguration(Path.Combine(unitContext.DataDirectory, "Global")),
                    DefaultBackup = new LocalDirectoryConfiguration(Path.Combine(unitContext.DataDirectory, "Global/Backup")),
                });

                // Journal
                crystalContext.SetJournal(new SimpleJournalConfiguration(new GlobalDirectoryConfiguration("Journal")));

                var storageConfiguration = new SimpleStorageConfiguration(
                    new GlobalDirectoryConfiguration("MainStorage")/*,
                    new GlobalDirectoryConfiguration("BackupStorage")*/)
                with
                {
                    NumberOfHistoryFiles = 2,
                };

                // Register FirstData configuration.
                crystalContext.AddCrystal<FirstData>(
                    new CrystalConfiguration()
                    {
                        RequiredForLoading = true,
                        SaveFormat = SaveFormat.Utf8, // The format is utf8 text.
                        SaveInterval = TimeSpan.FromSeconds(5), // Save every 5 seconds.
                        NumberOfFileHistories = 2,
                        // FileConfiguration = new LocalFileConfiguration("Local/SimpleExample/SimpleData.tinyhand"), // Specify the file name to save.
                        FileConfiguration = new GlobalFileConfiguration(), // Specify the file name to save.
                        // BackupFileConfiguration = new GlobalFileConfiguration("Backup/"),
                        // StorageConfiguration = storageConfiguration,
                    });

                crystalContext.AddCrystal<SecondData>(
                    new CrystalConfiguration()
                    {
                        SaveFormat = SaveFormat.Utf8, // The format is utf8 text.
                        NumberOfFileHistories = 2, // No history file.
                        FileConfiguration = new GlobalFileConfiguration(), // Specify the file name to save.
                        StorageConfiguration = storageConfiguration,
                    });
            })
            .PostConfigure(context =>
            {
            });

        var unit = builder.Build(); // Build.
        TinyhandSerializer.ServiceProvider = unit.Context.ServiceProvider;
        var crystalizer = unit.Context.ServiceProvider.GetRequiredService<Crystalizer>(); // Obtains a Crystalizer instance for data storage operations.
        await crystalizer.PrepareAndLoad(); // Prepare resources for storage operations and read data from files.

        var data = unit.Context.ServiceProvider.GetRequiredService<FirstData>();

        Console.WriteLine($"Estimated size: SemaphoreLock {EstimateSize.Class<SemaphoreLock>()} bytes");
        Console.WriteLine($"Estimated size: SemaphoreSlim {EstimateSize.Constructor(() => new SemaphoreSlim(0))} bytes");
        Console.WriteLine($"Estimated size: FirstData {EstimateSize.Class<FirstData>()} bytes");
        Console.WriteLine($"Estimated size: SizeTestClass {EstimateSize.Class<SizeTestClass>()} bytes");
        Console.WriteLine($"Estimated size: StoragePoint<DoubleClass> {EstimateSize.Class<StoragePoint<DoubleClass>>()} bytes");
        Console.WriteLine($"Estimated size: StorageObject {EstimateSize.Constructor(() => CrystalData.Internal.StorageObject.UnsafeConstructor())} bytes");
        Console.WriteLine($"Estimated size: StorageId {EstimateSize.Struct<StorageId>()} bytes");
        // Console.WriteLine($"Estimated size: StorageObjec2 {EstimateSize.Class<StorageObjec2>()} bytes");
        // Console.WriteLine($"Estimated size: StoragePoint<> {EstimateSize.Struct<StoragePoint>()} bytes");
        // Console.WriteLine($"Estimated size: SpSecondClass {SizeMatters.EstimateClass<SpSecondClass>()} bytes");

        Console.WriteLine($"Load {data.ToString()}");
        data.Id += 1;
        using (var sdataScope = await data.DoubleStorage.TryLock())
        {
            if (sdataScope.IsValid)
            {
                sdataScope.Data.Double += 0.1;
            }
        }

        Console.WriteLine($"Save {data.ToString()}");

        var crystal = unit.Context.ServiceProvider.GetRequiredService<ICrystal<FirstData>>();
        crystal.AddToSaveQueue(2);
        // await Task.Delay(10_000);

        data = unit.Context.ServiceProvider.GetRequiredService<FirstData>();
        Console.WriteLine($"Data {data.ToString()}");

        crystal = unit.Context.ServiceProvider.GetRequiredService<ICrystal<FirstData>>();
        var crystal2 = unit.Context.ServiceProvider.GetRequiredService<ICrystal<SecondData>>();
        // await crystal2.PrepareAndLoad(false);

        var data2 = unit.Context.ServiceProvider.GetRequiredService<SecondData>();
        var classStorage = data2.ClassStorage;
        using (var d = await classStorage.TryLock(AcquisitionMode.GetOrCreate))
        {
            if (d.IsValid)
            {
                d.Data.Double += 1.2d;
                d.Data.Int += 11;
            }
        }

        // await Task.Delay(3000);

        /*var mem = GC.GetTotalMemory(false);
        var bb = new StorageObject[10_000_000];
        for (int i = 0; i < bb.Length; i++)
        {
            bb[i] = new StorageObject();
        }

        var mem2 = GC.GetTotalMemory(false);
        Console.WriteLine($"Memory {mem2 / 1000000}, {(mem2 - mem) / 1_000_000}");*/

        // data.DoubleStorage.Set(await data.DoubleStorage.TryGet() + 0.1);
        var doubleClass = await data.DoubleStorage.TryGet();
        if (doubleClass is not null)
        {
            doubleClass.Double += 0.1;
        }

        await data2.ClassStorage.StoreData(StoreMode.TryRelease);
        data2.ClassStorage.DeleteLatestStorageForTest();
        await crystalizer.StoreJournal();

        Console.WriteLine($"First: {await data.DoubleStorage.TryGet()}");
        Console.WriteLine($"Second: {await data2.ClassStorage.TryGet()}");

        var spClassGoshujin = data2.SpClassGoshujin;
        using (var scope = await spClassGoshujin.TryLock(1, AcquisitionMode.GetOrCreate))
        {
            if (scope.Data is { } spClass)
            {
                spClass.Name += "ox";
                Console.WriteLine(spClass.Name);
            }
        }

        // await spClassGoshujin.TryDelete(1);
        var spc = new SpClassPoint();
        // spc = spClassGoshujin.FindFirst(1);

        var goshujinStorage = data2.GoshujinStorage;
        using (var gs = await goshujinStorage.TryLock())
        {
            if (gs.Data is { } gs2)
            {

                // await gs2.TryLock(12, AcquisitionMode.GetOrCreate);
                using (var gs3 = await gs2.TryLock(12, AcquisitionMode.GetOrCreate))
                {
                }

                await gs2.Delete(12);
            }
        }

        using (var sc = await goshujinStorage.TryLock(123, AcquisitionMode.GetOrCreate))
        {
            if (sc.Data is SpClass spClass)
            {
                spClass.Name = "Hello";
            }
        }

        var sd = await goshujinStorage.Find(100);
        sd = await goshujinStorage.Find(123);

        Console.WriteLine($"MemoryUsage: {crystalizer.StorageControl.MemoryUsage}");
        var r2 = await goshujinStorage.StoreData(StoreMode.ForceRelease);
        Console.WriteLine($"MemoryUsage: {crystalizer.StorageControl.MemoryUsage}");

        using (var sc = await sd!.TryLock())
        {
            if (sc.Data is { } spClass)
            {
                spClass.Name = "123";
            }
        }

        using (var sc = await goshujinStorage.TryLock(123, AcquisitionMode.GetOrCreate))
        {
            if (sc.Data is { } spClass)
            {
                var name = spClass.Name;
            }
        }

        crystalizer.Dump();
        // await crystalizer.StoreAndRelease();
        await crystalizer.StoreAndRip();
        Console.WriteLine($"MemoryUsage: {crystalizer.StorageControl.MemoryUsage}");
    }
}
