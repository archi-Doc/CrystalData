// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Arc.Crypto;
using CrystalData;
using Microsoft.Extensions.DependencyInjection;
using Tinyhand;
using Xunit;

namespace xUnitTest;

public static class TestHelper
{
    public static async Task<ICrystal<TData>> CreateAndStartCrystal<TData>(bool addStorage = false)
        where TData : class, ITinyhandSerialize<TData>, ITinyhandReconstruct<TData>
    {
        var directory = $"Crystal[{RandomVault.Pseudo.NextUInt32():x4}]";
        StorageConfiguration storageConfiguration = addStorage ?
            new SimpleStorageConfiguration(new LocalDirectoryConfiguration(Path.Combine(directory, "Storage"))) :
            EmptyStorageConfiguration.Default;

        var builder = new CrystalControl.Builder();
        builder.ConfigureCrystal(context =>
        {
            context.SetJournal(new SimpleJournalConfiguration(new LocalDirectoryConfiguration(Path.Combine(directory, "Journal"))));
            context.AddCrystal<TData>(
                new(SavePolicy.Manual, new LocalFileConfiguration(Path.Combine(directory, "Test.tinyhand")))
                {
                    SaveFormat = SaveFormat.Utf8,
                    NumberOfFileHistories = 5,
                    StorageConfiguration = storageConfiguration,
                });
        });

        var unit = builder.Build();
        var crystalizer = unit.Context.ServiceProvider.GetRequiredService<Crystalizer>();

        var crystal = crystalizer.GetCrystal<TData>();
        var result = await crystalizer.PrepareAndLoadAll(false);
        result.Is(CrystalResult.Success);
        return crystal;
    }

    public static async Task UnloadAndDeleteAll(ICrystal crystal)
    {
        await crystal.Crystalizer.SaveAndUnloadAll();
        var stat = crystal.Crystalizer.Memory.GetStat();
        stat.MemoryUsage.Is(0);
        stat.MemoryCount.Is(0);
        await crystal.Crystalizer.DeleteAll();

        if (crystal.Crystalizer.JournalConfiguration is SimpleJournalConfiguration journalConfiguration)
        {
            Directory.Delete(journalConfiguration.DirectoryConfiguration.Path, true);
        }

        var directory = Path.GetDirectoryName(crystal.CrystalConfiguration.FileConfiguration.Path);
        if (directory is not null)
        {
            Directory.EnumerateFileSystemEntries(directory).Any().IsFalse(); // Directory is empty
            Directory.Delete(directory, true);
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
