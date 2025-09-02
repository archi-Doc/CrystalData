// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrystalData;

internal class CrystalUnitContext : ICrystalUnitContext, IUnitCustomContext
{
    void ICrystalUnitContext.AddCrystal<TData>(CrystalConfiguration configuration)
    {
        this.typeToCrystalConfiguration[typeof(TData)] = configuration;
    }

    void ICrystalUnitContext.SetJournal(JournalConfiguration configuration)
    {
        this.journalConfiguration = configuration;
    }

    bool ICrystalUnitContext.TrySetJournal(JournalConfiguration configuration)
    {
        if (this.journalConfiguration != EmptyJournalConfiguration.Default)
        {
            return false;
        }

        this.journalConfiguration = configuration;
        return true;
    }

    void IUnitCustomContext.Configure(IUnitConfigurationContext context)
    {
        foreach (var x in this.typeToCrystalConfiguration)
        {// This is slow, but it is Singleton anyway.
            // Singleton: ICrystal<T> => Crystalizer.GetCrystal<T>()
            context.Services.Add(ServiceDescriptor.Singleton(typeof(ICrystal<>).MakeGenericType(x.Key), provider => provider.GetRequiredService<Crystalizer>().GetCrystal(x.Key)));

            if (x.Key.GetCustomAttribute<TinyhandObjectAttribute>() is { } attribute &&
                attribute.UseServiceProvider)
            {// Tinyhand invokes ServiceProvider during object creation, which leads to recursive calls.
            }
            else
            {// Singleton: T => Crystalizer.GetObject<T>()
                foreach (var y in context.Services)
                {
                    if (y.ServiceType == x.Key && y.Lifetime == ServiceLifetime.Singleton)
                    {// Registered as singleton
                        // Although it is a Singleton, UseServiceProvider is not set to true (which is a code defect), so CrystalData will treat it as a Singleton.
                        x.Value.IsSingleton = true;
                    }
                }

                context.Services.TryAdd(ServiceDescriptor.Transient(x.Key, provider => provider.GetRequiredService<Crystalizer>().GetObject(x.Key)));
            }
        }

        if (!context.TryGetOptions<CrystalizerConfiguration>(out var configuration))
        {// New
            configuration = new CrystalizerConfiguration(this.typeToCrystalConfiguration, this.journalConfiguration);
            context.SetOptions(configuration);
        }
        else
        {// Existing
            foreach (var x in this.typeToCrystalConfiguration)
            {
                configuration.CrystalConfigurations[x.Key] = x.Value;
            }
        }

        var options = new CrystalizerOptions() with { DataDirectory = context.DataDirectory, };
        context.SetOptions(options);
    }

    private Dictionary<Type, CrystalConfiguration> typeToCrystalConfiguration = new();
    private JournalConfiguration journalConfiguration = EmptyJournalConfiguration.Default;
}
