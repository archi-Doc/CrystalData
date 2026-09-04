// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Provides bucket-scoped credentials to storage backends.
/// </summary>
public interface IStorageKey
{
    bool AddKey(string bucket, AccessKeyPair accessKeyPair);

    bool TryGetKey(string bucket, out AccessKeyPair accessKeyPair);
}
