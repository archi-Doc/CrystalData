// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public readonly struct CrystalSourceAndResult
{
    public CrystalSourceAndResult(CrystalSource source, CrystalResult result)
    {
        this.Source = source;
        this.Result = result;
    }

    public bool IsSuccess => this.Result == CrystalResult.Success;

    public readonly CrystalSource Source;

    public readonly CrystalResult Result;
}
