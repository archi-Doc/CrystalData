// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Microsoft.Extensions.DependencyInjection;

namespace CrystalData;

public readonly struct CrystalNew<T>
{
    public CrystalNew(T instance)
    {
        this.Instance = instance;
    }

    public readonly T Instance;
}

internal class UnitCrystalContext : IUnitCrystalContext, IUnitCustomContext
{
    void IUnitCrystalContext.AddCrystal<TData>(CrystalConfiguration configuration)
    {
        this.typeToCrystalConfiguration[typeof(TData)] = configuration;
    }

    /*bool IUnitCrystalContext.TryAddCrystal<TData>(CrystalConfiguration configuration)
    {
        return this.typeToCrystalConfiguration.TryAdd(typeof(TData), configuration);
    }*/

    void IUnitCrystalContext.SetJournal(JournalConfiguration configuration)
    {
        this.journalConfiguration = configuration;
    }

    bool IUnitCrystalContext.TrySetJournal(JournalConfiguration configuration)
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

            /*if (x.Key.GetCustomAttribute<TinyhandObjectAttribute>() is { } attribute &&
                attribute.UseServiceProvider)
            {// Tinyhand invokes ServiceProvider during object creation, which leads to recursive calls.
            }
            else
            {// Singleton: T => Crystalizer.GetObject<T>()
                context.Services.TryAdd(ServiceDescriptor.Singleton(x.Key, provider => provider.GetRequiredService<Crystalizer>().GetObject(x.Key)));
            }*/
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

        var options = new CrystalizerOptions();
        options.RootPath = context.RootDirectory;
        context.SetOptions(options);
    }

    private Dictionary<Type, CrystalConfiguration> typeToCrystalConfiguration = new();
    private JournalConfiguration journalConfiguration = EmptyJournalConfiguration.Default;
}
