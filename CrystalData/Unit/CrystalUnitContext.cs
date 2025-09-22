// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrystalData;

internal class CrystalUnitContext : ICrystalConfigurationContext, IUnitCustomContext
{
    void ICrystalConfigurationContext.SetCrystalOptions(CrystalOptions options)
    {
        this.crystalOptions = options;
    }

    void ICrystalConfigurationContext.AddCrystal<TData>(CrystalConfiguration configuration)
    {
        this.typeToCrystalConfiguration[typeof(TData)] = configuration;
    }

    void ICrystalConfigurationContext.SetJournal(JournalConfiguration configuration)
    {
        this.journalConfiguration = configuration;
    }

    bool ICrystalConfigurationContext.TrySetJournal(JournalConfiguration configuration)
    {
        if (this.journalConfiguration != EmptyJournalConfiguration.Default)
        {
            return false;
        }

        this.journalConfiguration = configuration;
        return true;
    }

    void IUnitCustomContext.ProcessContext(IUnitConfigurationContext context)
    {
        if (this.crystalOptions is null)
        {
            this.crystalOptions = new CrystalOptions() with { DataDirectory = context.DataDirectory, };
        }

        context.SetOptions(this.crystalOptions);

        // var serviceTypeToLifetime = context.Services.ToDictionary(x => x.ServiceType, x => x.Lifetime);
        Dictionary<Type, ServiceLifetime> serviceTypeToLifetime = new();
        foreach (var x in context.Services)
        {// If duplicate keys exist, overwrite with the later key/value.
            serviceTypeToLifetime[x.ServiceType] = x.Lifetime;
        }

        foreach (var x in this.typeToCrystalConfiguration)
        {// This is slow, but it is Singleton anyway.
            // Singleton: ICrystal<T> => CrystalControl.GetCrystal<T>()
            context.Services.TryAdd(ServiceDescriptor.Singleton(typeof(ICrystal<>).MakeGenericType(x.Key), provider => provider.GetRequiredService<CrystalControl>().GetCrystal(x.Key)));

            /*if (x.Key.GetCustomAttribute<TinyhandObjectAttribute>() is { } attribute &&
                attribute.UseServiceProvider)
            {// Tinyhand invokes ServiceProvider during object creation, which leads to recursive calls.
            }
            else
            {// Singleton: T => CrystalControl.GetObject<T>()
                foreach (var y in context.Services)
                {
                    if (y.ServiceType == x.Key && y.Lifetime == ServiceLifetime.Singleton)
                    {// Registered as singleton
                        // Although it is a Singleton, UseServiceProvider is not set to true (which is a code defect), so CrystalData will treat it as a Singleton.
                        x.Value.IsSingleton = true;
                        break;
                    }
                }

                context.Services.TryAdd(ServiceDescriptor.Transient(x.Key, provider => provider.GetRequiredService<CrystalControl>().GetObject(x.Key)));
            }*/

            if (x.Key.GetCustomAttribute<TinyhandObjectAttribute>() is { } attribute &&
                attribute.UseServiceProvider)
            {// Tinyhand invokes ServiceProvider during object creation, which leads to recursive calls.
            }
            else
            {
                if (serviceTypeToLifetime.TryGetValue(x.Key, out var lifetime))
                {
                    if (lifetime == ServiceLifetime.Singleton)
                    {// Although it is a Singleton, UseServiceProvider is not set to true (which is a code defect), so CrystalData will treat it as a Singleton.
                        x.Value.IsSingleton = true;
                    }
                }
                else
                {// Singleton: T => CrystalControl.GetObject<T>()
                    context.Services.TryAdd(ServiceDescriptor.Transient(x.Key, provider => provider.GetRequiredService<CrystalControl>().GetData(x.Key)));
                }
            }
        }

        var crystalControlConfiguration = context.GetOptions<CrystalControlConfiguration>();
        crystalControlConfiguration = crystalControlConfiguration with
        {
            JournalConfiguration = this.journalConfiguration,
        };

        foreach (var x in this.typeToCrystalConfiguration)
        {
            crystalControlConfiguration.CrystalConfigurations[x.Key] = x.Value;
        }

        context.SetOptions(crystalControlConfiguration);
    }

    private CrystalOptions? crystalOptions;
    private Dictionary<Type, CrystalConfiguration> typeToCrystalConfiguration = new();
    private JournalConfiguration journalConfiguration = EmptyJournalConfiguration.Default;
}
