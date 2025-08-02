// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1210 // Using directives should be ordered alphabetically by namespace

global using Arc.Collections;
global using Arc.Crypto;
global using Arc.Threading;
global using Arc.Unit;
global using Tinyhand;
global using ValueLink;
using CrystalData.Storage;
using CrystalData.UserInterface;
using Microsoft.Extensions.DependencyInjection;
using static CrystalData.CrystalControl;

namespace CrystalData;

public class CrystalControl
{
    public class Builder : UnitBuilder<Unit>
    {// Builder class for customizing dependencies.
        public Builder()
            : base()
        {
            this.Preload(context =>
            {
                if (context.FirstBuilderRun)
                {
                    this.LoadStrings();
                }
            });

            this.Configure(context =>
            {
                if (context.FirstBuilderRun)
                {
                    // Main services
                    context.AddSingleton<CrystalControl>();
                    context.AddSingleton<CrystalizerConfiguration>();
                    context.AddSingleton<CrystalizerOptions>();
                    context.AddSingleton<Crystalizer>();
                    context.AddSingleton<StorageControl>();
                    context.Services.AddSingleton<StorageControl>(serviceProvider => StorageControl.Default);
                    context.AddSingleton<IStorageKey, StorageKey>();
                    context.TryAddSingleton<ICrystalDataQuery, CrystalDataQueryDefault>();
                }

                var crystalContext = context.GetCustomContext<CrystalUnitContext>();
                foreach (var x in this.crystalActions)
                {
                    x(crystalContext);
                }
            });
        }

        public new Builder Preload(Action<IUnitPreloadContext> @delegate)
        {
            base.Preload(@delegate);
            return this;
        }

        public new Builder Configure(Action<IUnitConfigurationContext> @delegate)
        {
            base.Configure(@delegate);
            return this;
        }

        public override Builder SetupOptions<TOptions>(Action<IUnitSetupContext, TOptions> @delegate)
            where TOptions : class
        {
            return (Builder)base.SetupOptions(@delegate);
        }

        public Builder ConfigureCrystal(Action<ICrystalUnitContext> @delegate)
        {
            this.crystalActions.Add(@delegate);
            return this;
        }

        private void LoadStrings()
        {// Load strings
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            try
            {
                HashedString.LoadAssembly(null, asm, "UserInterface.Strings.strings-en.tinyhand");
                HashedString.LoadAssembly("ja", asm, "UserInterface.Strings.strings-ja.tinyhand");
            }
            catch
            {
            }
        }

        private List<Action<ICrystalUnitContext>> crystalActions = new();
    }

    public class Unit : BuiltUnit
    {// Unit class for customizing behaviors.
        public Unit(UnitContext context)
            : base(context)
        {
        }
    }

    public CrystalControl(UnitContext unitContext)
    {
        this.unitContext = unitContext;
    }

    public bool ExaltationOfIntegrality { get; } = true; // ZenItz by Baxter.

    private readonly UnitContext unitContext;
}
