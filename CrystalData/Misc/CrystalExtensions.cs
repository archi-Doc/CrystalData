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
            SaveFormat.Binary => CrystalControl.BinaryExtension,
            SaveFormat.Utf8 => CrystalControl.Utf8Extension,
            _ => CrystalControl.BinaryExtension,
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
    public static bool TryGetJournalWriter(this IStructuralObject obj, out TinyhandWriter writer)
    {
        if (obj.StructuralRoot is not null)
        {
            return obj.StructuralRoot.TryGetJournalWriter(JournalType.Record, out writer);
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
}
