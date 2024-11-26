// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Runtime.CompilerServices;
using CrystalData;
using Microsoft.Extensions.DependencyInjection;
using Tinyhand.IO;

public static class CrystalExtensions
{
    /// <summary>
    /// Retrieve the data managed by CrystalData via <see cref="IServiceProvider"/>.
    /// </summary>
    /// <typeparam name="TData">The type of data.</typeparam>
    /// <param name="provider">The <see cref="IServiceProvider"/> to retrieve the service object from.</param>
    /// <returns>The data of type <typeparamref name="TData"/>.</returns>
    public static TData GetRequiredData<TData>(this IServiceProvider provider)
        where TData : class, ITinyhandSerializable<TData>, ITinyhandReconstructable<TData>
    {
        return ((ICrystal<TData>)provider.GetRequiredService(typeof(ICrystal<TData>))).Data;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetJournalWriter(this IStructualObject obj, out TinyhandWriter writer)
    {
        if (obj.StructualRoot is not null)
        {
            return obj.StructualRoot.TryGetJournalWriter(JournalType.Record, out writer);
        }
        else
        {
            writer = default;
            return false;
        }
    }

    public static bool IsSuccess(this CrystalResult result)
        => result == CrystalResult.Success;

    public static bool IsFailure(this CrystalResult result)
        => result != CrystalResult.Success;

    public static bool IsUnload(this UnloadMode unloadMode)
        => unloadMode != UnloadMode.NoUnload;

    /// <summary>
    /// Compares this value with a specified <see langword="ulong"/> value in a situation where the variables are cyclical (i.e., they reset to zero after reaching their maximum value).
    /// </summary>
    /// <param name="value1">value1.</param>
    /// <param name="value2">value2.</param>
    /// <returns>-1: <paramref name="value1"/> is less than <paramref name="value2"/>.<br/>
    /// 0: <paramref name="value1"/> and <paramref name="value2"/> are equal.<br/>
    /// -1: <paramref name="value1"/> is greater than <paramref name="value2"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CircularCompareTo(this ulong value1, ulong value2)
    {
        var diff = value1 - value2;
        if (diff > 0x8000_0000_0000_0000)
        {
            return -1;
        }
        else if (diff > 0)
        {
            return 1;
        }
        else
        {
            return 0;
        }
    }

    /// <summary>
    /// Compares this value with a specified <see langword="uint"/> value in a situation where the variables are cyclical (i.e., they reset to zero after reaching their maximum value).
    /// </summary>
    /// <param name="value1">value1.</param>
    /// <param name="value2">value2.</param>
    /// <returns>-1: <paramref name="value1"/> is less than <paramref name="value2"/>.<br/>
    /// 0: <paramref name="value1"/> and <paramref name="value2"/> are equal.<br/>
    /// -1: <paramref name="value1"/> is greater than <paramref name="value2"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CircularCompareTo(this uint value1, uint value2)
    {
        var diff = value1 - value2;
        if (diff > 0x8000_0000)
        {
            return -1;
        }
        else if (diff > 0)
        {
            return 1;
        }
        else
        {
            return 0;
        }
    }
}
