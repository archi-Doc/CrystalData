// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public readonly struct CrystalMemoryOwnerResult
{
    public CrystalMemoryOwnerResult(CrystalResult result, BytePool.RentReadOnlyMemory data)
    {
        this.Result = result;
        this.Data = data;
    }

    public CrystalMemoryOwnerResult(CrystalResult result)
    {
        this.Result = result;
        this.Data = default;
    }

    public void Return() => this.Data.Return();

    public bool IsSuccess => this.Result == CrystalResult.Success;

    public bool IsFailure => this.Result != CrystalResult.Success;

    public readonly CrystalResult Result;

    public readonly BytePool.RentReadOnlyMemory Data;

    public override string ToString()
        => $"{this.Result} Data[{this.Data.Memory.Length}]";
}
