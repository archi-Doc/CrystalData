// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public interface IStorageKey
{
    bool AddKey(string bucket, AccessKeyPair accessKeyPair);

    bool TryGetKey(string bucket, out AccessKeyPair accessKeyPair);
}
