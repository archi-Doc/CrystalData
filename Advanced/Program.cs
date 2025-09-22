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

        // var product = await FirstExample();
        // var product = await SecondExample();
        // var product = await SaveTimingExample();
        // var product = await ConfigurationExample();
        // var product = await JournalExample();
        // var product = await PathExample();
        // var product = await BackupExample();
        // var product = await ServiceProviderExample();
        // var product = await IntegratedExample();
        // var product = await DefaultExample();
        var product = await StoragePointExample();

        ThreadCore.Root.Terminate();
        if (product is not null)
        {// Save all data managed by CrystalData.
            await product.Context.ServiceProvider.GetRequiredService<CrystalControl>().StoreAndRip();
        }

        await ThreadCore.Root.WaitForTerminationAsync(-1); // Wait for the termination infinitely.
        if (product?.Context.ServiceProvider.GetService<UnitLogger>() is { } unitLogger)
        {// Flush the buffered logs and then shut down the logger.
            await unitLogger.FlushAndTerminate();
        }

        ThreadCore.Root.TerminationEvent.Set(); // The termination process is complete (#1).
    }
}
