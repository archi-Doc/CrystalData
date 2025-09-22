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
        var crystalControl = unit.Context.ServiceProvider.GetRequiredService<CrystalControl>();

        var crystal = crystalControl.GetCrystal<TData>();
        var result = await crystalControl.PrepareAndLoad(false);
        return crystal;
    }

    public static async Task StoreAndReleaseAndDelete(ICrystal crystal)
    {
        var crystalControl = crystal.CrystalControl;
        await crystalControl.StoreAndRelease();
        await crystalControl.DeleteAll();

        if (crystal.CrystalControl.JournalConfiguration is SimpleJournalConfiguration journalConfiguration)
        {
            crystalControl.DeleteDirectory(journalConfiguration.DirectoryConfiguration);
        }

        var directory = Path.GetDirectoryName(crystal.CrystalConfiguration.FileConfiguration.Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.Delete(directory, true);
        }

        if (crystalControl.Options.GlobalDirectory is not EmptyDirectoryConfiguration)
        {
            crystalControl.DeleteDirectory(crystalControl.Options.GlobalDirectory);
        }
    }
}
