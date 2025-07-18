// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public interface IStorageData
{
    Task<bool> StoreData(UnloadMode unloadMode);

    Type DataType { get; }
}
