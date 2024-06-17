// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public readonly struct CrystalObjectResult<T>
{
    public CrystalObjectResult(CrystalResult result, T? obj)
    {
        this.Result = result;
        this.Object = obj;
    }

    public CrystalObjectResult(CrystalResult result)
    {
        this.Result = result;
        this.Object = default;
    }

    public bool IsSuccess => this.Result == CrystalResult.Success;

    public bool IsFailure => this.Result != CrystalResult.Success;

    public readonly CrystalResult Result;

    public readonly T? Object;
}
