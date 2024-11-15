// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Storage;

internal class StorageKey : IStorageKey
{
    public StorageKey()
    {
    }

    public bool AddKey(string bucket, AccessKeyPair accessKeyPair)
    {
        using (this.lockObject.EnterScope())
        {
            this.dictionary[bucket] = accessKeyPair;
            return true;
        }
    }

    public bool TryGetKey(string bucket, out AccessKeyPair accessKeyPair)
    {
        using (this.lockObject.EnterScope())
        {
            return this.dictionary.TryGetValue(bucket, out accessKeyPair);
        }
    }

    private Lock lockObject = new();
    private Dictionary<string, AccessKeyPair> dictionary = new();
}
