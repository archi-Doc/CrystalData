// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public interface IStoragePoint
{
    Task<bool> Save(UnloadMode unloadMode);

    Type DataType { get; }
}
