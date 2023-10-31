// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Storage;

public class StorageKey : IStorageKey
{
    public StorageKey()
    {
    }

    public bool AddKey(string bucket, AccessKeyPair accessKeyPair)
    {
        lock(this.syncObject)
        {
            this.dictionary[bucket] = accessKeyPair;
            return true;
        }
    }

    public bool TryGetKey(string bucket, out AccessKeyPair accessKeyPair)
    {
        lock (this.syncObject)
        {
            return this.dictionary.TryGetValue(bucket, out accessKeyPair);
        }
    }

    private object syncObject = new();
    private Dictionary<string, AccessKeyPair> dictionary = new();
}
