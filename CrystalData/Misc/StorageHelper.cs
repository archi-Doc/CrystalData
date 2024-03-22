// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public static partial class StorageHelper
{
    public const long Megabytes = 1024 * 1024;
    public const long Gigabytes = 1024 * 1024 * 1024;

    public static bool CheckPrimaryCrystal(ref ICrystal? primaryCrystal, ref ICrystal? callingCrystal)
    {
        if (callingCrystal is null)
        {// Force save
            return true;
        }
        else if (primaryCrystal is null || primaryCrystal.State == CrystalState.Deleted)
        {
            primaryCrystal = callingCrystal;
            return true;
        }
        else if (primaryCrystal == callingCrystal)
        {
            return true;
        }

        var primaryInterval = GetInterval(primaryCrystal);
        var interval = GetInterval(callingCrystal);
        if (primaryInterval > interval)
        {
            primaryCrystal = callingCrystal;
            return true;
        }
        else
        {
            return false;
        }

        TimeSpan GetInterval(ICrystal c)
        {
            return c.CrystalConfiguration.SavePolicy switch
            {
                SavePolicy.Manual => TimeSpan.FromMinutes(10),
                SavePolicy.Volatile => TimeSpan.MaxValue,
                SavePolicy.Periodic => c.CrystalConfiguration.SaveInterval,
                SavePolicy.OnChanged => TimeSpan.FromMinutes(1),
                _ => TimeSpan.MaxValue,
            };
        }
    }

    public static string ByteToString(long size)
    {
        // MaxValue = 9_223_372_036_854_775_807
        // B, K, M, G, T, P, E

        if (size < 1000)
        {
            return $"{size}B";
        }

        size /= 100;
        if (size < 100)
        {
            return $"{size / 10}.{size % 10}K";
        }

        size /= 10;
        if (size < 1000)
        {
            return $"{size}K";
        }

        size /= 100;
        if (size < 100)
        {
            return $"{size / 10}.{size % 10}M";
        }

        size /= 10;
        if (size < 1000)
        {
            return $"{size}M";
        }

        size /= 100;
        if (size < 100)
        {
            return $"{size / 10}.{size % 10}G";
        }

        size /= 10;
        if (size < 1000)
        {
            return $"{size}G";
        }

        size /= 100;
        if (size < 100)
        {
            return $"{size / 10}.{size % 10}T";
        }

        size /= 10;
        if (size < 1000)
        {
            return $"{size}T";
        }

        size /= 100;
        if (size < 100)
        {
            return $"{size / 10}.{size % 10}P";
        }

        size /= 10;
        if (size < 1000)
        {
            return $"{size}P";
        }

        size /= 100;
        if (size < 100)
        {
            return $"{size / 10}.{size % 10}E";
        }

        size /= 10;
        return $"{size}E";
    }
}
