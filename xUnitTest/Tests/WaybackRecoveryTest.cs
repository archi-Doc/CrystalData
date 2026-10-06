// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace xUnitTest.CrystalDataTest;

public class WaybackRecoveryTest
{
    [Fact]
    public async Task FailedWaybackSavePreservesOriginalSnapshotForRetry()
    {
        var directory = Directory.CreateTempSubdirectory("CrystalData-Wayback-").FullName;
        var dataDirectory = Path.Combine(directory, "data");
        var journalDirectory = Path.Combine(directory, "journal");
        var configuration = new CrystalConfiguration(new LocalFileConfiguration(Path.Combine(dataDirectory, "value.tinyhand")))
        {
            SaveFormat = SaveFormat.Utf8,
            NumberOfHistoryFiles = 1,
        };
        CrystalControl? control = null;
        string? blocker = null;
        try
        {
            control = CreateControl();
            Assert.Equal(CrystalResult.Success, await control.PrepareAndLoad(false));
            control.GetCrystal<PersistenceData>().Data.Value = 123;
            await control.StoreAndRip(TestContext.Current.CancellationToken);
            control = null;

            var originalFile = Assert.Single(Directory.GetFiles(dataDirectory, "value.*.tinyhand"));
            var waypointText = Path.GetFileNameWithoutExtension(originalFile)["value.".Length..];
            Assert.True(Waypoint.TryParse(waypointText, out var originalWaypoint));
            Assert.True(originalWaypoint.JournalPosition > Waypoint.ValidJournalPosition);

            Directory.Delete(journalDirectory, true);
            var targetWaypoint = new Waypoint(Waypoint.ValidJournalPosition, originalWaypoint.Hash, originalWaypoint.Plane);
            blocker = Path.Combine(dataDirectory, $"value.{targetWaypoint.ToBase32()}.tinyhand");
            Directory.CreateDirectory(blocker);
            control = CreateControl();

            Assert.NotEqual(CrystalResult.Success, await control.PrepareAndLoad(false));
            Assert.True(File.Exists(originalFile));
            Assert.Equal(CrystalState.Initial, control.GetCrystal<PersistenceData>().State);

            Directory.Delete(blocker);
            blocker = null;
            Assert.Equal(CrystalResult.Success, await control.PrepareAndLoad(false));
            Assert.Equal(123, control.GetCrystal<PersistenceData>().Data.Value);
            await control.StoreAndRip(TestContext.Current.CancellationToken);
            control = null;

            control = CreateControl();
            Assert.Equal(CrystalResult.Success, await control.PrepareAndLoad(false));
            Assert.Equal(123, control.GetCrystal<PersistenceData>().Data.Value);
        }
        finally
        {
            if (blocker is not null && Directory.Exists(blocker))
            {
                Directory.Delete(blocker);
            }

            try
            {
                if (control is not null)
                {
                    await control.StoreAndRip(TestContext.Current.CancellationToken);
                }
            }
            finally
            {
                TestHelper.TryDeleteDirectory(directory);
            }
        }

        CrystalControl CreateControl()
        {
            var product = new CrystalUnit.Builder().ConfigureCrystal(context =>
            {
                context.SetCrystalOptions(new CrystalOptions { DataDirectory = directory, });
                context.SetJournal(new SimpleJournalConfiguration(new LocalDirectoryConfiguration(journalDirectory)));
                context.AddCrystal<PersistenceData>(configuration);
            }).Build();
            return product.Context.ServiceProvider.GetRequiredService<CrystalControl>();
        }
    }
}
