// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Arc.Crypto;
using CrystalData;
using Microsoft.Extensions.DependencyInjection;
using Tinyhand;
using Xunit;

namespace xUnitTest;

public static class TestHelper
{
    private const int DefaultNumberOfFileHistories = 3;

    public static async Task<ICrystal<TData>> CreateAndStartCrystal<TData>(bool addStorage = false)
        where TData : class, ITinyhandSerializable<TData>, ITinyhandReconstructable<TData>
    {
        var directory = $"Crystal[{RandomVault.Default.NextUInt32():x4}]";
        StorageConfiguration storageConfiguration = addStorage ?
            new SimpleStorageConfiguration(new LocalDirectoryConfiguration(Path.Combine(directory, "Storage")))
            {
                NumberOfHistoryFiles = DefaultNumberOfFileHistories,
            }
            : EmptyStorageConfiguration.Default;

        var builder = new CrystalControl.Builder();
        builder.ConfigureCrystal(context =>
        {
            context.SetJournal(new SimpleJournalConfiguration(new LocalDirectoryConfiguration(Path.Combine(directory, "Journal"))));
            context.AddCrystal<TData>(
                new(new LocalFileConfiguration(Path.Combine(directory, "Test.tinyhand")))
                {
                    SaveFormat = SaveFormat.Utf8,
                    NumberOfFileHistories = DefaultNumberOfFileHistories,
                    StorageConfiguration = storageConfiguration,
                });
        });

        var unit = builder.Build();
        TinyhandSerializer.ServiceProvider = unit.Context.ServiceProvider;
        var crystalizer = unit.Context.ServiceProvider.GetRequiredService<Crystalizer>();
        crystalizer.StorageControl.MemoryUsage.Is(0);

        var crystal = crystalizer.GetCrystal<TData>();
        var result = await crystalizer.PrepareAndLoad(false);
        result.Is(CrystalResult.Success);
        return crystal;
    }

    public static async Task<ICrystal<TData>> CreateAndStartCrystal2<TData>()
        where TData : class, ITinyhandSerializable<TData>, ITinyhandReconstructable<TData>
    {
        var builder = new CrystalControl.Builder();
        builder.ConfigureCrystal(context =>
        {
            context.SetJournal(new SimpleJournalConfiguration(new GlobalDirectoryConfiguration("Journal")));
            context.AddCrystal<TData>(
                new(new GlobalFileConfiguration("Test.tinyhand"))
                {
                    SaveFormat = SaveFormat.Utf8,
                    NumberOfFileHistories = DefaultNumberOfFileHistories,
                    StorageConfiguration = new SimpleStorageConfiguration(new GlobalDirectoryConfiguration("Storage")),
                });
        });

        builder.PostConfigure(context =>
        {
            context.SetOptions(context.GetOptions<CrystalizerOptions>() with
            {
                GlobalDirectory = new LocalDirectoryConfiguration($"Crystal[{RandomVault.Default.NextUInt32():x4}]"),
            });
        });

        var unit = builder.Build();
        var crystalizer = unit.Context.ServiceProvider.GetRequiredService<Crystalizer>();
        crystalizer.StorageControl.MemoryUsage.Is(0);

        var crystal = crystalizer.GetCrystal<TData>();
        var result = await crystalizer.PrepareAndLoad(false);
        result.Is(CrystalResult.Success);
        return crystal;
    }

    public static async Task StoreAndReleaseAndDelete(ICrystal crystal)
    {
        var crystalizer = crystal.Crystalizer;
        await crystalizer.StoreAndRelease();
        crystalizer.StorageControl.MemoryUsage.Is(0);
        await crystalizer.DeleteAll();

        if (crystal.Crystalizer.JournalConfiguration is SimpleJournalConfiguration journalConfiguration)
        {
            crystalizer.DeleteDirectory(journalConfiguration.DirectoryConfiguration);
        }

        var directory = Path.GetDirectoryName(crystal.CrystalConfiguration.FileConfiguration.Path);
        if (!string.IsNullOrEmpty(directory))
        {
            StorageHelper.ContainsAnyFile(directory).IsFalse(); // Directory is empty
            Directory.Delete(directory, true);
        }

        if (crystalizer.Options.GlobalDirectory is not EmptyDirectoryConfiguration)
        {
            crystalizer.DeleteDirectory(crystalizer.Options.GlobalDirectory);
        }
    }

    public static bool DataEquals(this CrystalMemoryResult dataResult, Span<byte> span)
    {
        return dataResult.Data.Span.SequenceEqual(span);
    }

    public static bool ByteArrayEquals(byte[]? array1, byte[]? array2, int length)
    {
        if (array1 == null || array2 == null)
        {
            return false;
        }
        else if (array1.Length < length || array2.Length < length)
        {
            return false;
        }

        for (var n = 0; n < length; n++)
        {
            if (array1[n] != array2[n])
            {
                return false;
            }
        }

        return true;
    }
}
