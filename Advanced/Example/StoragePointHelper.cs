// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Arc.Crypto;

namespace CrystalData;

public static partial class StoragePointHelper2
{
    private const int DefaultNumberOfFileHistories = 3;

    public static async Task<ICrystal<TData>> CreateAndStartCrystal<TData>()
        where TData : class, ITinyhandSerializable<TData>, ITinyhandReconstructable<TData>
    {
        var directory = $"Crystal[{RandomVault.Default.NextUInt32():x4}]";
        var storageConfiguration = new SimpleStorageConfiguration(new LocalDirectoryConfiguration(Path.Combine(directory, "Storage")))
        {
            NumberOfHistoryFiles = DefaultNumberOfFileHistories,
        };

        var builder = new CrystalUnit.Builder();
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

        var crystal = crystalizer.GetCrystal<TData>();
        var result = await crystalizer.PrepareAndLoad(false);
        return crystal;
    }

    public static async Task StoreAndReleaseAndDelete(ICrystal crystal)
    {
        var crystalizer = crystal.Crystalizer;
        await crystalizer.StoreAndRelease();
        await crystalizer.DeleteAll();

        if (crystal.Crystalizer.JournalConfiguration is SimpleJournalConfiguration journalConfiguration)
        {
            crystalizer.DeleteDirectory(journalConfiguration.DirectoryConfiguration);
        }

        var directory = Path.GetDirectoryName(crystal.CrystalConfiguration.FileConfiguration.Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.Delete(directory, true);
        }

        if (crystalizer.Options.GlobalDirectory is not EmptyDirectoryConfiguration)
        {
            crystalizer.DeleteDirectory(crystalizer.Options.GlobalDirectory);
        }
    }
}
