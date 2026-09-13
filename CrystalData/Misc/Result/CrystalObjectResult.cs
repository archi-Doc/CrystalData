// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Contains an operation result and an optional deserialized object.
/// </summary>
/// <typeparam name="T">The object type.</typeparam>
public readonly struct CrystalObjectResult<T>
{
    public CrystalObjectResult(CrystalResult result, T? data)
    {
        this.Result = result;
        this.Data = data;
    }

    public CrystalObjectResult(CrystalResult result)
    {
        this.Result = result;
        this.Data = default;
    }

    public bool IsSuccess => this.Result == CrystalResult.Success;

    public bool IsFailure => this.Result != CrystalResult.Success;

    public readonly CrystalResult Result;

    public readonly T? Data;
}
