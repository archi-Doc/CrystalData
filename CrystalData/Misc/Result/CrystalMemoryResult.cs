// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public readonly struct CrystalMemoryResult
{
    public CrystalMemoryResult(CrystalResult result, ReadOnlyMemory<byte> data)
    {
        this.Result = result;
        this.Data = data;
    }

    public CrystalMemoryResult(CrystalResult result)
    {
        this.Result = result;
        this.Data = default;
    }

    public bool IsSuccess => this.Result == CrystalResult.Success;

    public readonly CrystalResult Result;

    public readonly ReadOnlyMemory<byte> Data;

    public override string ToString()
        => $"{this.Result} ReadOnlyMemory:{this.Data.Length}";
}
