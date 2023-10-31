﻿// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1210 // Using directives should be ordered alphabetically by namespace

global using Arc.Crypto;
global using Arc.Threading;
global using Arc.Unit;
global using Tinyhand;
global using ValueLink;
using CrystalData.Storage;
using CrystalData.UserInterface;
using Microsoft.Extensions.DependencyInjection;

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
                    context.AddSingleton<IStorageKey, StorageKey>();
                    context.TryAddSingleton<ICrystalDataQuery, CrystalDataQueryDefault>();
                }

                var crystalContext = context.GetCustomContext<UnitCrystalContext>();
                foreach (var x in this.crystalActions)
                {
                    x(crystalContext);
                }
            });

            /*this.CustomConfigure = context =>
            {
                var crystalContext = context.GetCustomContext<UnitCrystalContext>();
                foreach (var x in this.crystalActions)
                {
                    x(crystalContext);
                }
            };*/
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

        public Builder ConfigureCrystal(Action<IUnitCrystalContext> @delegate)
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

        private List<Action<IUnitCrystalContext>> crystalActions = new();
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
