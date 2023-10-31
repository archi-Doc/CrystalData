// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public readonly struct AccessKeyPair : IEquatable<AccessKeyPair>
{
    public const char Separator = '=';

    public AccessKeyPair()
    {
        this.AccessKeyId = string.Empty;
        this.SecretAccessKey = string.Empty;
    }

    public AccessKeyPair(string accessKeyId, string secretAccessKey)
    {
        this.AccessKeyId = accessKeyId;
        this.SecretAccessKey = secretAccessKey;
    }

    public static bool TryParse(string text, out AccessKeyPair accessKeyPair)
    {// AccessKeyId=SecretAccessKey
        var array = text.Split(Separator);
        if (array.Length < 2)
        {
            accessKeyPair = default;
            return false;
        }

        accessKeyPair = new(array[0], array[1]);
        return true;
    }

    public static bool TryParse(string text, out string bucket, out AccessKeyPair accessKeyPair)
    {// Bucket=AccessKeyId=SecretAccessKey
        var array = text.Split(Separator);
        if (array.Length < 3)
        {
            bucket = string.Empty;
            accessKeyPair = default;
            return false;
        }

        bucket = array[0];
        accessKeyPair = new(array[1], array[2]);
        return true;
    }

    public readonly string AccessKeyId;

    public readonly string SecretAccessKey;

    public override int GetHashCode()
        => HashCode.Combine(this.AccessKeyId, this.SecretAccessKey);

    public override string ToString()
        => $"{this.AccessKeyId}{Separator}{this.SecretAccessKey}";

    public bool Equals(AccessKeyPair other)
        => this.AccessKeyId == other.AccessKeyId &&
        this.SecretAccessKey == other.SecretAccessKey;
}
