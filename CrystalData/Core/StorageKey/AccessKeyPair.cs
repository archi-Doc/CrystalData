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
        if (text is null)
        {
            accessKeyPair = default;
            return false;
        }

        var separatorIndex = text.IndexOf(Separator, StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            accessKeyPair = default;
            return false;
        }

        accessKeyPair = new(text[..separatorIndex], text[(separatorIndex + 1)..]);
        return true;
    }

    public static bool TryParse(string text, out string bucket, out AccessKeyPair accessKeyPair)
    {// Bucket=AccessKeyId=SecretAccessKey
        if (text is null)
        {
            bucket = string.Empty;
            accessKeyPair = default;
            return false;
        }

        var firstSeparator = text.IndexOf(Separator, StringComparison.Ordinal);
        var secondSeparator = -1;
        if (firstSeparator >= 0)
        {
            var relativeIndex = text.AsSpan(firstSeparator + 1).IndexOf(Separator);
            if (relativeIndex >= 0)
            {
                secondSeparator = firstSeparator + relativeIndex + 1;
            }
        }

        if (secondSeparator < 0)
        {
            bucket = string.Empty;
            accessKeyPair = default;
            return false;
        }

        bucket = text[..firstSeparator];
        accessKeyPair = new(text[(firstSeparator + 1)..secondSeparator], text[(secondSeparator + 1)..]);
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
