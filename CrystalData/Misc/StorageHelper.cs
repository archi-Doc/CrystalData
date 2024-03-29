﻿// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Runtime.CompilerServices;

namespace CrystalData;

public static partial class StorageHelper
{
    public const char Slash = '/';
    public const char Backslash = '\\';
    public const char Colon = ':';
    public const string SlashString = "/";
    public const string BackslashString = "\\";
    public const string ColonString = ":";
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

    public static bool EndsWith_SlashInsensitive(string path, string value)
    {
        if (path.Length < value.Length)
        {
            return false;
        }

        var pathSpan = path.AsSpan(path.Length - value.Length);
        var valueSpan = value.AsSpan();
        for (var i = valueSpan.Length - 1; i >= 0; i--)
        {
            if (pathSpan[i] == Slash || pathSpan[i] == Backslash)
            {
                if (valueSpan[i] == Slash || valueSpan[i] == Backslash)
                {
                    continue;
                }
                else
                {
                    return false;
                }
            }
            else if (pathSpan[i] != valueSpan[i])
            {
                return false;
            }
        }

        return true;
    }

    public static string GetPathNotRoot(string path)
    {
        if (!Path.IsPathRooted(path))
        {
            return path;
        }

        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root))
        {
            return path;
        }

        return path.Substring(root.Length);
    }

    public static (string Directory, string File) PathToDirectoryAndFile(string path)
    {
        var span = path.AsSpan();
        for (var i = span.Length - 1; i >= 1; i--)
        {
            if (span[i] == Slash || span[i] == Backslash)
            {
                var st = span[0..0].ToString();
                var st2 = span[0..1].ToString();
                var st3 = span[span.Length..].ToString();
                return (span[0..i].ToString(), span[(i + 1)..].ToString());
            }
        }

        return (string.Empty, path);
    }

    public static string CombineWithBackslash(string path1, string path2)
        => CombineWith(Backslash, path1, path2);

    public static string CombineWithSlash(string path1, string path2)
        => CombineWith(Slash, path1, path2);

    public static string CombineWith(char separator, string path1, string path2)
    {
        var omitLast1 = false;
        if (path1.Length > 0)
        {
            if (IsSeparator(path1[path1.Length - 1]))
            {
                omitLast1 = true;
            }
        }

        var omitFirst2 = false;
        if (path2.Length > 0)
        {
            if (IsSeparator(path2[0]))
            {
                omitFirst2 = true;
            }
        }

        if (omitLast1)
        {
            if (omitFirst2)
            {// path1/ + /path2
                return path1 + path2.Substring(1);
            }
            else
            {// path1/ + path2
                return path1 + path2;
            }
        }
        else
        {
            if (omitFirst2)
            {// path1 + /path2
                return path1 + path2;
            }
            else
            {// path1 + path2
                return path1 + separator + path2;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EndsWithSlashOrBackslash(string path)
        => path.EndsWith(Slash) || path.EndsWith(Backslash);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSeparator(char c)
        => c == Slash || c == Backslash || c == Colon;

    public static bool IsDirectoryWritable(string directory)
    {
        try
        {
            using (var fs = File.Create(Path.Combine(directory, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
