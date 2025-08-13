// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

global using Arc.Threading;
global using Arc.Unit;
global using CrystalData;
global using Microsoft.Extensions.DependencyInjection;
global using Tinyhand;
global using ValueLink;

namespace QuickStart;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {// Console window closing or process terminated.
            ThreadCore.Root.Terminate(); // Send a termination signal to the root.
            ThreadCore.Root.TerminationEvent.WaitOne(2000); // Wait until the termination process is complete (#1).
        };

        Console.CancelKeyPress += (s, e) =>
        {// Ctrl+C pressed
            e.Cancel = true;
            ThreadCore.Root.Terminate(); // Send a termination signal to the root.
        };

        // var unit = await FirstExample();
        var unit = await SecondExample();
        // var unit = await SaveTimingExample();
        // var unit = await ConfigurationExample();
        // var unit = await JournalExample();
        // var unit = await PathExample();
        // var unit = await BackupExample();
        // var unit = await ServiceProviderExample();
        // var unit = await IntegratedExample();
        // var unit = await DefaultExample();

        ThreadCore.Root.Terminate();
        if (unit is not null)
        {// Save all data managed by CrystalData.
            await unit.Context.ServiceProvider.GetRequiredService<Crystalizer>().SaveAndRip();
        }

        await ThreadCore.Root.WaitForTerminationAsync(-1); // Wait for the termination infinitely.
        if (unit?.Context.ServiceProvider.GetService<UnitLogger>() is { } unitLogger)
        {// Flush the buffered logs and then shut down the logger.
            await unitLogger.FlushAndTerminate();
        }

        ThreadCore.Root.TerminationEvent.Set(); // The termination process is complete (#1).
    }
}
