// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData.UserInterface;

namespace CrystalData;

public readonly struct PrepareParam
{
    internal static PrepareParam NoQuery<TData>(CrystalControl crystalControl)
        => new(crystalControl, typeof(TData), false);

    internal static PrepareParam New<TData>(CrystalControl crystalControl, bool useQuery)
        => new(crystalControl, typeof(TData), useQuery);

    internal static PrepareParam New(CrystalControl crystalControl, Type dataType, bool useQuery)
        => new(crystalControl, dataType, useQuery);

    private PrepareParam(CrystalControl crystalControl, Type dataType, bool useQuery)
    {
        this.CrystalControl = crystalControl;
        this.DataType = dataType;
        this.UseQuery = useQuery;
    }

    /*public void RegisterConfiguration(PathConfiguration configuration, out bool newlyRegistered)
    {
        this.CrystalControl.CrystalCheck.RegisterDataAndConfiguration(
            new(this.DataTypeName, configuration),
            out newlyRegistered);
    }*/

    public readonly CrystalControl CrystalControl;

    public readonly bool UseQuery;

    public readonly Type DataType;

    public ICrystalDataQuery Query
        => this.UseQuery ? this.CrystalControl.Query : this.CrystalControl.QueryContinue;

    public string DataTypeName
        => this.DataType.FullName ?? string.Empty;
}

/*public class PrepareParam
{
    internal static PrepareParam NoQuery<TData>(CrystalControl crystalControl)
        => new(crystalControl, typeof(TData), false);

    internal static PrepareParam New<TData>(CrystalControl crystalControl, bool useQuery)
        => new(crystalControl, typeof(TData), useQuery);

    private PrepareParam(CrystalControl crystalControl, Type dataType, bool useQuery)
    {
        this.CrystalControl = crystalControl;
        this.DataType = dataType;
        this.DataTypeName = this.DataType.FullName ?? string.Empty;
        this.UseQuery = useQuery;
    }

    public void RegisterConfiguration(PathConfiguration configuration, out bool newlyRegistered)
    {
        this.CrystalControl.CrystalCheck.RegisterDataAndConfiguration(
            new(this.DataTypeName, configuration),
            out newlyRegistered);
    }

    public CrystalControl CrystalControl { get; }

    public bool UseQuery { get; }

    public ICrystalDataQuery Query
        => this.UseQuery ? this.CrystalControl.Query : this.CrystalControl.QueryContinue;

    public Type DataType { get; }

    public string DataTypeName { get; }
}*/
