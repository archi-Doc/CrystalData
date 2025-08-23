// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Runtime.CompilerServices;
using CrystalData;
using Microsoft.Extensions.DependencyInjection;
using Tinyhand.IO;

public static class CrystalExtensions
{
    /// <summary>
    /// Converts the <see cref="SaveFormat"/> to its corresponding file extension.
    /// </summary>
    /// <param name="saveFormat">The save format to convert.</param>
    /// <returns>The file extension corresponding to the save format.</returns>
    public static string ToExtension(this SaveFormat saveFormat)
        => saveFormat switch
        {
            SaveFormat.Binary => Crystalizer.BinaryExtension,
            SaveFormat.Utf8 => Crystalizer.Utf8Extension,
            _ => Crystalizer.BinaryExtension,
        };

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

    /// <summary>
    /// Tries to get a <see cref="TinyhandWriter"/> for the journal.
    /// </summary>
    /// <param name="obj">The structural object.</param>
    /// <param name="writer">The <see cref="TinyhandWriter"/> to be retrieved.</param>
    /// <returns><c>true</c> if the writer was successfully retrieved; otherwise, <c>false</c>.</returns>
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

    /// <summary>
    /// Determines whether the <see cref="CrystalResult"/> is a success.
    /// </summary>
    /// <param name="result">The result to check.</param>
    /// <returns><c>true</c> if the result is <see cref="CrystalResult.Success"/>; otherwise, <c>false</c>.</returns>
    public static bool IsSuccess(this CrystalResult result)
        => result == CrystalResult.Success;

    /// <summary>
    /// Determines whether the <see cref="CrystalResult"/> is a failure.
    /// </summary>
    /// <param name="result">The result to check.</param>
    /// <returns><c>true</c> if the result is not <see cref="CrystalResult.Success"/>; otherwise, <c>false</c>.</returns>
    public static bool IsFailure(this CrystalResult result)
        => result != CrystalResult.Success;

    /// <summary>
    /// Compares this value with a specified <see langword="ulong"/> value in a situation where the variables are cyclical (i.e., they reset to zero after reaching their maximum value).
    /// </summary>
    /// <param name="value1">The first value to compare.</param>
    /// <param name="value2">The second value to compare.</param>
    /// <returns>-1 if <paramref name="value1"/> is less than <paramref name="value2"/>; 0 if they are equal; 1 if <paramref name="value1"/> is greater than <paramref name="value2"/>.</returns>
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
    /// <param name="value1">The first value to compare.</param>
    /// <param name="value2">The second value to compare.</param>
    /// <returns>-1 if <paramref name="value1"/> is less than <paramref name="value2"/>; 0 if they are equal; 1 if <paramref name="value1"/> is greater than <paramref name="value2"/>.</returns>
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
