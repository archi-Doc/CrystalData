// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

global using Arc.Threading;
global using Arc.Unit;
global using CrystalData;
global using Microsoft.Extensions.DependencyInjection;
global using Tinyhand;
global using ValueLink;
using Arc;

namespace QuickStart;

public partial class Program
{
    private static ExecutionRoot? root;

    public static async Task Main(string[] args)
    {
        AppCloseHandler.Set(() =>
        {// Closing the console window or terminating the process.
            root?.RequestTermination(); // Send a termination signal to the root.
            root?.WaitForTermination(TimeSpan.FromSeconds(2)).Wait();
        });

        Console.CancelKeyPress += (s, e) =>
        {// Ctrl+C pressed.
            e.Cancel = true;
            root?.RequestTermination(); // Send a termination signal to the root.
        };

        root = new();

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
        // var product = await StoragePointExample();
        var product = await QuickStart.Evolution.EvolutionExample.Program();

        root.RequestTermination();
        if (product is not null)
        {// Save all data managed by CrystalData.
            await product.Context.ServiceProvider.GetRequiredService<CrystalControl>().StoreAndRip();
        }

        await root.WaitForTermination(); // Wait for the termination infinitely.
        if (product?.Context.ServiceProvider.GetService<LogUnit>() is { } logUnit)
        {// Flush the buffered logs and then shut down the logger.
            await logUnit.FlushAndTerminate();
        }
    }
}
