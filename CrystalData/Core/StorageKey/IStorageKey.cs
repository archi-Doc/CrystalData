// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Provides bucket-scoped credentials to storage backends.
/// </summary>
public interface IStorageKey
{
    /// <summary>
    /// Registers or replaces the credentials for a bucket.
    /// </summary>
    /// <param name="bucket">The bucket name.</param>
    /// <param name="accessKeyPair">The credentials.</param>
    /// <returns>Whether the credentials were registered.</returns>
    bool AddKey(string bucket, AccessKeyPair accessKeyPair);

    /// <summary>
    /// Looks up the credentials for a bucket.
    /// </summary>
    /// <param name="bucket">The bucket name.</param>
    /// <param name="accessKeyPair">The credentials, if found.</param>
    /// <returns>Whether credentials were found.</returns>
    bool TryGetKey(string bucket, out AccessKeyPair accessKeyPair);
}
