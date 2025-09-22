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

namespace CrystalData;

public class CrystalUnit
{
    #region Builder

    public class Builder : UnitBuilder<Product>
    {// Builder class for customizing dependencies.
        public Builder()
            : base()
        {
            this.PreConfigure(context =>
            {
                this.LoadStrings();
            });

            this.Configure(context =>
            {
                // Main services
                context.AddSingleton<CrystalUnit>();
                context.AddSingleton<CrystalControlConfiguration>();
                context.AddSingleton<CrystalOptions>();
                context.AddSingleton<CrystalControl>();
                context.AddTransient<StorageControl>();
                context.AddTransient<StorageMap>();
                context.AddSingleton<IStorageKey, StorageKey>();
                context.TryAddSingleton<ICrystalDataQuery, CrystalDataQueryDefault>();

                var crystalContext = context.GetCustomContext<CrystalUnitContext>();
                foreach (var x in this.crystalActions)
                {
                    x(crystalContext);
                }

                foreach (var x in this.crystalActions2)
                {
                    x(context, crystalContext);
                }
            });
        }

        public new Builder PreConfigure(Action<IUnitPreConfigurationContext> @delegate)
        {
            base.PreConfigure(@delegate);
            return this;
        }

        public new Builder Configure(Action<IUnitConfigurationContext> @delegate)
        {
            base.Configure(@delegate);
            return this;
        }

        public new Builder PostConfigure(Action<IUnitPostConfigurationContext> @delegate)
        {
            base.PostConfigure(@delegate);
            return this;
        }

        public Builder ConfigureCrystal(Action<ICrystalConfigurationContext> @delegate)
        {
            this.crystalActions.Add(@delegate);
            return this;
        }

        public Builder ConfigureCrystal(Action<IUnitConfigurationContext, ICrystalConfigurationContext> @delegate)
        {
            this.crystalActions2.Add(@delegate);
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

        private List<Action<ICrystalConfigurationContext>> crystalActions = new();
        private List<Action<IUnitConfigurationContext, ICrystalConfigurationContext>> crystalActions2 = new();
    }

    #endregion

    #region Product

    public class Product : BuiltUnit
    {// Unit class for customizing behaviors.
        public Product(UnitContext context)
            : base(context)
        {
        }
    }

    #endregion

    public CrystalUnit(UnitContext unitContext)
    {
        this.unitContext = unitContext;
    }

    public bool ExaltationOfIntegrality { get; } = true; // ZenItz by Baxter.

    private readonly UnitContext unitContext;
}
