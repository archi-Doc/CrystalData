// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData.UserInterface;

namespace CrystalData;

public readonly struct PrepareParam
{
    internal static PrepareParam NoQuery<TData>(Crystalizer crystalizer)
        => new(crystalizer, typeof(TData), false);

    internal static PrepareParam New<TData>(Crystalizer crystalizer, bool useQuery)
        => new(crystalizer, typeof(TData), useQuery);

    internal static PrepareParam New(Crystalizer crystalizer, Type dataType, bool useQuery)
        => new(crystalizer, dataType, useQuery);

    private PrepareParam(Crystalizer crystalizer, Type dataType, bool useQuery)
    {
        this.Crystalizer = crystalizer;
        this.DataType = dataType;
        this.UseQuery = useQuery;
    }

    /*public void RegisterConfiguration(PathConfiguration configuration, out bool newlyRegistered)
    {
        this.Crystalizer.CrystalCheck.RegisterDataAndConfiguration(
            new(this.DataTypeName, configuration),
            out newlyRegistered);
    }*/

    public readonly Crystalizer Crystalizer;

    public readonly bool UseQuery;

    public readonly Type DataType;

    public ICrystalDataQuery Query
        => this.UseQuery ? this.Crystalizer.Query : this.Crystalizer.QueryContinue;

    public string DataTypeName
        => this.DataType.FullName ?? string.Empty;
}

/*public class PrepareParam
{
    internal static PrepareParam NoQuery<TData>(Crystalizer crystalizer)
        => new(crystalizer, typeof(TData), false);

    internal static PrepareParam New<TData>(Crystalizer crystalizer, bool useQuery)
        => new(crystalizer, typeof(TData), useQuery);

    private PrepareParam(Crystalizer crystalizer, Type dataType, bool useQuery)
    {
        this.Crystalizer = crystalizer;
        this.DataType = dataType;
        this.DataTypeName = this.DataType.FullName ?? string.Empty;
        this.UseQuery = useQuery;
    }

    public void RegisterConfiguration(PathConfiguration configuration, out bool newlyRegistered)
    {
        this.Crystalizer.CrystalCheck.RegisterDataAndConfiguration(
            new(this.DataTypeName, configuration),
            out newlyRegistered);
    }

    public Crystalizer Crystalizer { get; }

    public bool UseQuery { get; }

    public ICrystalDataQuery Query
        => this.UseQuery ? this.Crystalizer.Query : this.Crystalizer.QueryContinue;

    public Type DataType { get; }

    public string DataTypeName { get; }
}*/
