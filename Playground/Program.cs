// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Arc.Threading;
using Arc.Unit;
using CrystalData;
using Microsoft.Extensions.DependencyInjection;
using Tinyhand;
using Tinyhand.IO;
using ValueLink;

namespace Sandbox;

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
{
    public FirstData()
    {
    }

    [Key(0)] // The key attribute specifies the index at serialization
    public int Id { get; set; }

    [Key(1)]
    [DefaultValue("Hoge")] // The default value for the name property.
    public string Name { get; set; } = string.Empty;

    [Key(2)]
    public StoragePoint<int> IntStorage { get; set; } = new();

    public override string ToString()
        => $"Id: {this.Id}, Name: {this.Name}";
}

[TinyhandObject(Structual = true)]
// [TinyhandObject(Structual = true, UseServiceProvider = true)]
public partial class SecondData
{
    public SecondData()
    {
    }

    [Key(0)]
    public StoragePoint<double> DoubleStorage { get; set; } = new();

    [Key(1)]
    public SpClassPoint.GoshujinClass SpClassGoshujin { get; set; } = new();

    [Key(2)]
    public StoragePoint<SpClassPoint.GoshujinClass> GoshujinStorage { get; set; } = new();

    public override string ToString()
        => $"Second: {this.DoubleStorage.TryGet()}";
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
    public static ValueTask<DataScope<SpClass>> TryLock(this CrystalData.StoragePoint<SpClassPoint.GoshujinClass> storagePoint, int id, AcquisitionMode acquisitionMode, CancellationToken cancellationToken = default)
        => TryLock(storagePoint, id, acquisitionMode, ValueLinkGlobal.LockTimeout, cancellationToken);

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
        SpClassPoint? point = default;
        using (var scope = await storagePoint.TryLock(AcquisitionMode.Get, timeout, cancellationToken).ConfigureAwait(false))
        {
            if (scope.Data is { } g) point = g.FindFirst(key);
            else return scope.Result;
        }

        if (point is null) return DataScopeResult.NotFound;
        await point.Delete(forceDeleteAfter).ConfigureAwait(false);
        return DataScopeResult.Success;
    }
}

[TinyhandObject(Structual = true)]
public partial class SpClass
{
    public SpClass()
    {
    }

    [Key(0)]
    public string Name { get; set; } = string.Empty;
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
            .SetupOptions<CrystalizerOptions>((context, options) =>
            {
                context.GetOptions<UnitOptions>(out var unitOptions);
                if (unitOptions is not null)
                {
                    options.GlobalDirectory = new LocalDirectoryConfiguration(Path.Combine(unitOptions.DataDirectory, "Global"));
                }
            })
            .ConfigureCrystal(context =>
            {
                // context.SetJournal(new SimpleJournalConfiguration(new GlobalDirectoryConfiguration("Journal")));

                var storageConfiguration = new SimpleStorageConfiguration(
                    new GlobalDirectoryConfiguration("MainStorage"),
                    new GlobalDirectoryConfiguration("BackupStorage"))
                with
                {
                    NumberOfHistoryFiles = 2,
                };

                // Register FirstData configuration.
                context.AddCrystal<FirstData>(
                    new CrystalConfiguration()
                    {
                        RequiredForLoading = true,
                        SavePolicy = SavePolicy.Manual, // The timing of saving data is controlled by the application.
                        SaveFormat = SaveFormat.Utf8, // The format is utf8 text.
                        NumberOfFileHistories = 0, // No history file.
                        // FileConfiguration = new LocalFileConfiguration("Local/SimpleExample/SimpleData.tinyhand"), // Specify the file name to save.
                        FileConfiguration = new GlobalFileConfiguration(), // Specify the file name to save.
                        StorageConfiguration = storageConfiguration,
                    });

                context.AddCrystal<SecondData>(
                    new CrystalConfiguration()
                    {
                        SavePolicy = SavePolicy.Manual, // The timing of saving data is controlled by the application.
                        SaveFormat = SaveFormat.Utf8, // The format is utf8 text.
                        NumberOfFileHistories = 2, // No history file.
                        FileConfiguration = new GlobalFileConfiguration(), // Specify the file name to save.
                        StorageConfiguration = storageConfiguration,
                    });
            });

        var unit = builder.Build(); // Build.
        TinyhandSerializer.ServiceProvider = unit.Context.ServiceProvider;
        var crystalizer = unit.Context.ServiceProvider.GetRequiredService<Crystalizer>(); // Obtains a Crystalizer instance for data storage operations.
        await crystalizer.PrepareAndLoadAll(true); // Prepare resources for storage operations and read data from files.

        var data = unit.Context.ServiceProvider.GetRequiredService<FirstData>();

        Console.WriteLine($"Load {data.ToString()}");
        data.Id += 1;
        Console.WriteLine($"Save {data.ToString()}");

        var crystal = unit.Context.ServiceProvider.GetRequiredService<ICrystal<FirstData>>();
        data = crystal.Data;
        data.Id += 1;
        Console.WriteLine($"Crystal {data.ToString()}");

        await crystal.Store(StoreMode.ForceRelease);
        Console.WriteLine($"Unload {crystal.Data.ToString()}");
        crystal.Data.Id++;
        Console.WriteLine($"Unload++ {crystal.Data.ToString()}");

        data = unit.Context.ServiceProvider.GetRequiredService<FirstData>();
        Console.WriteLine($"Data {data.ToString()}");

        crystal = unit.Context.ServiceProvider.GetRequiredService<ICrystal<FirstData>>();
        var crystal2 = unit.Context.ServiceProvider.GetRequiredService<ICrystal<SecondData>>();
        Console.WriteLine($"{crystal.CrystalConfiguration.StorageConfiguration.NumberOfHistoryFiles}, {crystal2.CrystalConfiguration.StorageConfiguration.NumberOfHistoryFiles}");

        var data2 = unit.Context.ServiceProvider.GetRequiredService<SecondData>();
        var doubleStorage = data2.DoubleStorage;
        using (var d = await doubleStorage.TryLock(AcquisitionMode.GetOrCreate))
        {
        }

        data.IntStorage.Set(await data.IntStorage.TryGet() + 1);
        data2.DoubleStorage.Set(await data2.DoubleStorage.TryGet() + 1.2);
        Console.WriteLine($"First: {await data.IntStorage.TryGet()}");
        Console.WriteLine($"Second: {await data2.DoubleStorage.TryGet()}");

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

                await gs2.TryDelete(12);
            }
        }

        await goshujinStorage.Delete();
        goshujinStorage.Set(new());

        using (var sc = await goshujinStorage.TryLock(123, AcquisitionMode.GetOrCreate))
        {
        }


        //await crystalizer.Store(); // Save all data.
        await crystalizer.StoreAndRelease();
        Console.WriteLine($"MemoryUsage: {crystalizer.StorageControl.MemoryUsage}");
    }
}
