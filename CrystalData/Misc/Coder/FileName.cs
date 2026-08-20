// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

namespace CrystalData;

#pragma warning disable SA1202 // Elements should be ordered by access
#pragma warning disable SA1204 // Static elements should appear before instance elements
#pragma warning disable SA1611 // Element parameters should be documented
#pragma warning disable SA1615 // Element return value should be documented
#pragma warning disable SA1642 // Constructor summary documentation should begin with standard text
#pragma warning disable SA1519 // Braces should not be omitted from multi-line child statement

/// <summary>
/// Encodes data into systematic Reed-Solomon shards over GF(256).
/// Any <see cref="DataShardCount"/> shards are sufficient to recover the original data.
/// </summary>
/// <remarks>
/// The encoded buffer consists of all data shards followed by all parity shards.
/// This type is thread-safe after construction.
/// </remarks>
public sealed class RsCoder2
{
    public const int DefaultDataShardCount = 8;
    public const int DefaultParityShardCount = 4;

    private const int StackallocThreshold = 4096;

    private readonly byte[] parityRows;

    /// <summary>
    /// Initializes a new instance of the <see cref="RsCoder2"/> class.
    /// </summary>
    /// <param name="dataShardCount">The number of data shards.</param>
    /// <param name="parityShardCount">The number of parity shards.</param>
    public RsCoder2(
        int dataShardCount = DefaultDataShardCount,
        int parityShardCount = DefaultParityShardCount)
    {
        if (dataShardCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(dataShardCount));
        }

        if (parityShardCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parityShardCount));
        }

        var shardCount = dataShardCount + parityShardCount;
        if (shardCount > GaloisField2.Size)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parityShardCount),
                "The total number of shards must not exceed 256.");
        }

        this.DataShardCount = dataShardCount;
        this.ParityShardCount = parityShardCount;
        this.ShardCount = shardCount;

        this.parityRows =
            GC.AllocateUninitializedArray<byte>(
                dataShardCount * parityShardCount);

        this.GenerateParityRows();
    }

    /// <summary>
    /// Gets the number of data shards.
    /// </summary>
    public int DataShardCount { get; }

    /// <summary>
    /// Gets the number of parity shards.
    /// </summary>
    public int ParityShardCount { get; }

    /// <summary>
    /// Gets the total number of shards.
    /// </summary>
    public int ShardCount { get; }

    /// <summary>
    /// Gets the shard length required for the specified data length.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetShardLength(int dataLength)
    {
        if (dataLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dataLength));
        }

        if (dataLength == 0)
        {
            return 0;
        }

        var n = this.DataShardCount;
        return (dataLength / n) + ((dataLength % n) == 0 ? 0 : 1);
    }

    /// <summary>
    /// Gets the encoded buffer length required for the specified data length.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetEncodedLength(int dataLength)
        => checked(this.GetShardLength(dataLength) * this.ShardCount);

    /// <summary>
    /// Encodes data and returns a newly allocated encoded buffer.
    /// </summary>
    public byte[] Encode(ReadOnlySpan<byte> source)
    {
        var encoded =
            GC.AllocateUninitializedArray<byte>(
                this.GetEncodedLength(source.Length));

        this.Encode(source, encoded);
        return encoded;
    }

    /// <summary>
    /// Encodes data into the specified buffer.
    /// </summary>
    public unsafe void Encode(
        ReadOnlySpan<byte> source,
        Span<byte> destination)
    {
        var shardLength = this.GetShardLength(source.Length);
        var requiredLength = checked(shardLength * this.ShardCount);

        if (destination.Length < requiredLength)
        {
            throw new ArgumentException(
                "The destination buffer is too small.",
                nameof(destination));
        }

        if (source.IsEmpty)
        {
            return;
        }

        var n = this.DataShardCount;
        var m = this.ParityShardCount;
        var dataLength = shardLength * n;

        destination = destination[..requiredLength];

        // Data shards are stored directly at the beginning.
        source.CopyTo(destination);

        // Zero only the padding area.
        destination.Slice(
            source.Length,
            dataLength - source.Length).Clear();

        fixed (byte* pEncoded = destination)
        fixed (byte* pParity = this.parityRows)
        fixed (byte* pMultiply = GaloisField2.Tables)
        {
            for (var parityIndex = 0;
                parityIndex < m;
                parityIndex++)
            {
                var destinationShard =
                    pEncoded + ((n + parityIndex) * shardLength);

                var coefficients =
                    pParity + (parityIndex * n);

                var initialized = false;

                for (var dataIndex = 0;
                    dataIndex < n;
                    dataIndex++)
                {
                    var coefficient = coefficients[dataIndex];

                    if (coefficient == 0)
                    {
                        continue;
                    }

                    var sourceShard =
                        pEncoded + (dataIndex * shardLength);

                    if (!initialized)
                    {
                        MultiplyCopy(
                            destinationShard,
                            sourceShard,
                            shardLength,
                            coefficient,
                            pMultiply);

                        initialized = true;
                    }
                    else
                    {
                        XorMultiply(
                            destinationShard,
                            sourceShard,
                            shardLength,
                            coefficient,
                            pMultiply);
                    }
                }

                if (!initialized)
                {
                    new Span<byte>(
                        destinationShard,
                        shardLength).Clear();
                }
            }
        }
    }

    /// <summary>
    /// Decodes the original data and returns a newly allocated buffer.
    /// </summary>
    public byte[] Decode(
        ReadOnlySpan<byte> shards,
        ReadOnlySpan<bool> shardAvailable,
        int dataLength)
    {
        if (dataLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dataLength));
        }

        var destination =
            GC.AllocateUninitializedArray<byte>(dataLength);

        this.Decode(
            shards,
            shardAvailable,
            dataLength,
            destination);

        return destination;
    }

    /// <summary>
    /// Decodes the original data.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// There are not enough shards to recover the original data.
    /// </exception>
    public void Decode(
        ReadOnlySpan<byte> shards,
        ReadOnlySpan<bool> shardAvailable,
        int dataLength,
        Span<byte> destination)
    {
        if (!this.TryDecode(
            shards,
            shardAvailable,
            dataLength,
            destination))
        {
            throw new InvalidDataException(
                "There are not enough shards to recover the original data.");
        }
    }

    /// <summary>
    /// Attempts to decode the original data.
    /// </summary>
    /// <returns>
    /// true if the original data was recovered; otherwise, false.
    /// </returns>
    public unsafe bool TryDecode(
        ReadOnlySpan<byte> shards,
        ReadOnlySpan<bool> shardAvailable,
        int dataLength,
        Span<byte> destination)
    {
        if (dataLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dataLength));
        }

        if (shardAvailable.Length < this.ShardCount)
        {
            throw new ArgumentException(
                "The shard availability span is too small.",
                nameof(shardAvailable));
        }

        if (destination.Length < dataLength)
        {
            throw new ArgumentException(
                "The destination buffer is too small.",
                nameof(destination));
        }

        var shardLength = this.GetShardLength(dataLength);
        var requiredLength =
            checked(shardLength * this.ShardCount);

        if (shards.Length < requiredLength)
        {
            throw new ArgumentException(
                "The encoded shard buffer is too small.",
                nameof(shards));
        }

        if (dataLength == 0)
        {
            return true;
        }

        destination = destination[..dataLength];
        shards = shards[..requiredLength];

        if (shards.Overlaps(destination, out var overlapOffset) &&
            overlapOffset != 0)
        {
            throw new ArgumentException(
                "The destination must not partially overlap the encoded buffer.");
        }

        var dataCount = this.DataShardCount; // 8

        // Only shards containing actual source bytes matter here.
        var usedDataShards =
            (dataLength / shardLength) +
            ((dataLength % shardLength) == 0 ? 0 : 1);

        var allRequiredDataAvailable = true;

        for (var i = 0; i < usedDataShards; i++)
        {
            if (!shardAvailable[i])
            {
                allRequiredDataAvailable = false;
                break;
            }
        }

        // Extremely common fast path.
        if (allRequiredDataAvailable)
        {
            shards[..dataLength].CopyTo(destination);
            return true;
        }

        var availableCount = 0;
        for (var i = 0; i < this.ShardCount; i++)
        {
            if (shardAvailable[i])
            {
                availableCount++;
            }
        }

        if (availableCount < dataCount)
        {
            return false;
        }

        var matrixWidth = dataCount << 1; // 8 -> 16
        var matrixLength = checked(dataCount * matrixWidth); // 8 x 16 -> 256
        var scratchLength = checked(matrixLength + dataCount); // 256 + 8

        byte[]? rented = null;
        scoped Span<byte> scratch;

        if (scratchLength <= StackallocThreshold)
        {
            scratch = stackalloc byte[scratchLength];
        }
        else
        {
            rented = ArrayPool<byte>.Shared.Rent(scratchLength);
            scratch = rented.AsSpan(0, scratchLength);
        }

        try
        {
            var matrix = scratch[..matrixLength];
            var selected = scratch.Slice(matrixLength, dataCount);

            matrix.Clear();

            var selectedCount = 0;
            for (var shardIndex = 0;
                shardIndex < this.ShardCount &&
                selectedCount < dataCount;
                shardIndex++)
            {
                if (!shardAvailable[shardIndex])
                {
                    continue;
                }

                selected[selectedCount] = (byte)shardIndex;

                var rowOffset = selectedCount * matrixWidth;

                var row = matrix.Slice(rowOffset, matrixWidth);
                if (shardIndex < dataCount)
                {
                    row[shardIndex] = 1;
                }
                else
                {
                    this.parityRows
                        .AsSpan((shardIndex - dataCount) * dataCount, dataCount)
                        .CopyTo(row);
                }

                row[dataCount + selectedCount] = 1;
                selectedCount++;
            }

            InvertAugmentedMatrix(matrix, dataCount);

            fixed (byte* pShards = shards)
            fixed (byte* pDestination = destination)
            fixed (byte* pMatrix = matrix)
            fixed (byte* pSelected = selected)
            fixed (byte* pMultiply = GaloisField2.Tables)
            {
                for (var dataIndex = 0; dataIndex < usedDataShards; dataIndex++)
                {
                    var outputOffset = dataIndex * shardLength;

                    var outputLength = Math.Min(shardLength, dataLength - outputOffset);

                    var output = pDestination + outputOffset;

                    if (shardAvailable[dataIndex])
                    {
                        var source = pShards + outputOffset;

                        new ReadOnlySpan<byte>(source, outputLength).CopyTo(
                                new Span<byte>(output, outputLength));

                        continue;
                    }

                    var inverseRow = pMatrix + (dataIndex * matrixWidth) + dataCount;

                    var initialized = false;

                    for (var selectedIndex = 0;
                        selectedIndex < dataCount;
                        selectedIndex++)
                    {
                        var coefficient =
                            inverseRow[selectedIndex];

                        if (coefficient == 0)
                        {
                            continue;
                        }

                        var sourceShardIndex =
                            pSelected[selectedIndex];

                        var source =
                            pShards +
                            (sourceShardIndex * shardLength);

                        if (!initialized)
                        {
                            MultiplyCopy(output, source, outputLength, coefficient, pMultiply);

                            initialized = true;
                        }
                        else
                        {
                            XorMultiply(output, source, outputLength, coefficient, pMultiply);
                        }
                    }

                    if (!initialized)
                    {
                        new Span<byte>(output, outputLength).Clear();
                    }
                }
            }

            return true;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Attempts to decode the original data directly into the beginning of
    /// the encoded buffer.
    /// </summary>
    public bool TryDecodeInPlace(
        Span<byte> shards,
        ReadOnlySpan<bool> shardAvailable,
        int dataLength)
        => this.TryDecode(
            shards,
            shardAvailable,
            dataLength,
            shards[..dataLength]);

    /// <summary>
    /// Decodes the original data directly into the beginning of the encoded buffer.
    /// </summary>
    public void DecodeInPlace(
        Span<byte> shards,
        ReadOnlySpan<bool> shardAvailable,
        int dataLength)
    {
        if (!this.TryDecodeInPlace(
            shards,
            shardAvailable,
            dataLength))
        {
            throw new InvalidDataException(
                "There are not enough shards to recover the original data.");
        }
    }

    /// <summary>
    /// Gets a shard from an encoded buffer.
    /// </summary>
    public ReadOnlySpan<byte> GetShard(
        ReadOnlySpan<byte> encoded,
        int dataLength,
        int shardIndex)
    {
        if ((uint)shardIndex >= (uint)this.ShardCount)
        {
            throw new ArgumentOutOfRangeException(nameof(shardIndex));
        }

        var shardLength = this.GetShardLength(dataLength);
        var requiredLength =
            checked(shardLength * this.ShardCount);

        if (encoded.Length < requiredLength)
        {
            throw new ArgumentException(
                "The encoded buffer is too small.",
                nameof(encoded));
        }

        return encoded.Slice(
            shardIndex * shardLength,
            shardLength);
    }

    /// <summary>
    /// Gets a writable shard from an encoded buffer.
    /// </summary>
    public Span<byte> GetShard(
        Span<byte> encoded,
        int dataLength,
        int shardIndex)
    {
        if ((uint)shardIndex >= (uint)this.ShardCount)
        {
            throw new ArgumentOutOfRangeException(nameof(shardIndex));
        }

        var shardLength = this.GetShardLength(dataLength);
        var requiredLength =
            checked(shardLength * this.ShardCount);

        if (encoded.Length < requiredLength)
        {
            throw new ArgumentException(
                "The encoded buffer is too small.",
                nameof(encoded));
        }

        return encoded.Slice(
            shardIndex * shardLength,
            shardLength);
    }

    public override string ToString()
        => $"RsCoder2 Data: {this.DataShardCount}, Parity: {this.ParityShardCount}";

    private void GenerateParityRows()
    {
        var n = this.DataShardCount;
        var matrixWidth = n << 1;
        var matrixLength = checked(n * matrixWidth);
        var scratchLength = checked(matrixLength + n);

        byte[]? rented = null;
        scoped Span<byte> scratch;

        if (scratchLength <= StackallocThreshold)
        {
            scratch = stackalloc byte[scratchLength];
        }
        else
        {
            rented =
                ArrayPool<byte>.Shared.Rent(scratchLength);

            scratch = rented.AsSpan(0, scratchLength);
        }

        try
        {
            var matrix = scratch[..matrixLength];
            var vandermonde =
                scratch.Slice(matrixLength, n);

            matrix.Clear();

            // Build the first n rows of the Vandermonde matrix and
            // augment them with the identity matrix.
            for (var rowIndex = 0;
                rowIndex < n;
                rowIndex++)
            {
                var row =
                    matrix.Slice(
                        rowIndex * matrixWidth,
                        matrixWidth);

                FillVandermondeRow(
                    rowIndex,
                    row[..n]);

                row[n + rowIndex] = 1;
            }

            // The right half becomes the inverse of the top matrix.
            InvertAugmentedMatrix(matrix, n);

            // generator = vandermonde * inverse(top)
            for (var parityIndex = 0;
                parityIndex < this.ParityShardCount;
                parityIndex++)
            {
                FillVandermondeRow(
                    n + parityIndex,
                    vandermonde);

                var destination =
                    this.parityRows.AsSpan(
                        parityIndex * n,
                        n);

                for (var column = 0;
                    column < n;
                    column++)
                {
                    byte value = 0;

                    for (var k = 0;
                        k < n;
                        k++)
                    {
                        var inverseValue =
                            matrix[
                                (k * matrixWidth) +
                                n +
                                column];

                        value ^=
                            GaloisField2.Multiply(
                                vandermonde[k],
                                inverseValue);
                    }

                    destination[column] = value;
                }
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillVandermondeRow(
        int x,
        Span<byte> row)
    {
        row[0] = 1;

        if (row.Length == 1)
        {
            return;
        }

        var fieldX = (byte)x;
        byte value = 1;

        for (var column = 1;
            column < row.Length;
            column++)
        {
            value =
                GaloisField2.Multiply(
                    value,
                    fieldX);

            row[column] = value;
        }
    }

    private static void InvertAugmentedMatrix(
        Span<byte> matrix,
        int n)
    {
        var width = n << 1;

        for (var column = 0;
            column < n;
            column++)
        {
            var pivotRow = column;

            while (
                pivotRow < n &&
                matrix[
                    (pivotRow * width) +
                    column] == 0)
            {
                pivotRow++;
            }

            if (pivotRow == n)
            {
                throw new InvalidOperationException(
                    "The Reed-Solomon matrix is singular.");
            }

            var currentOffset =
                column * width;

            if (pivotRow != column)
            {
                var pivotOffset =
                    pivotRow * width;

                for (var x = column;
                    x < width;
                    x++)
                {
                    var temp =
                        matrix[currentOffset + x];

                    matrix[currentOffset + x] =
                        matrix[pivotOffset + x];

                    matrix[pivotOffset + x] =
                        temp;
                }
            }

            var pivot =
                matrix[currentOffset + column];

            if (pivot != 1)
            {
                var inverse =
                    GaloisField2.Inverse(pivot);

                matrix[currentOffset + column] = 1;

                for (var x = column + 1;
                    x < width;
                    x++)
                {
                    matrix[currentOffset + x] =
                        GaloisField2.Multiply(
                            inverse,
                            matrix[currentOffset + x]);
                }
            }

            for (var row = 0;
                row < n;
                row++)
            {
                if (row == column)
                {
                    continue;
                }

                var rowOffset =
                    row * width;

                var factor =
                    matrix[rowOffset + column];

                if (factor == 0)
                {
                    continue;
                }

                matrix[rowOffset + column] = 0;

                for (var x = column + 1;
                    x < width;
                    x++)
                {
                    matrix[rowOffset + x] ^=
                        GaloisField2.Multiply(
                            factor,
                            matrix[currentOffset + x]);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void MultiplyCopy(
        byte* destination,
        byte* source,
        int length,
        byte coefficient,
        byte* multiply)
    {
        if (coefficient == 1)
        {
            Buffer.MemoryCopy(
                source,
                destination,
                length,
                length);

            return;
        }

        var table =
            multiply + (coefficient << 8);

        var i = 0;
        var limit = length - 7;

        for (; i < limit; i += 8)
        {
            destination[i] = table[source[i]];
            destination[i + 1] = table[source[i + 1]];
            destination[i + 2] = table[source[i + 2]];
            destination[i + 3] = table[source[i + 3]];
            destination[i + 4] = table[source[i + 4]];
            destination[i + 5] = table[source[i + 5]];
            destination[i + 6] = table[source[i + 6]];
            destination[i + 7] = table[source[i + 7]];
        }

        for (; i < length; i++)
        {
            destination[i] = table[source[i]];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void XorMultiply(
        byte* destination,
        byte* source,
        int length,
        byte coefficient,
        byte* multiply)
    {
        var i = 0;

        if (coefficient == 1)
        {
            var wordSize = sizeof(nuint);
            var limit = length - wordSize + 1;

            for (; i < limit; i += wordSize)
            {
                var value =
                    Unsafe.ReadUnaligned<nuint>(destination + i) ^
                    Unsafe.ReadUnaligned<nuint>(source + i);

                Unsafe.WriteUnaligned(
                    destination + i,
                    value);
            }

            for (; i < length; i++)
            {
                destination[i] ^= source[i];
            }

            return;
        }

        var table =
            multiply + (coefficient << 8);

        var unrolledLimit = length - 7;

        for (; i < unrolledLimit; i += 8)
        {
            destination[i] ^= table[source[i]];
            destination[i + 1] ^= table[source[i + 1]];
            destination[i + 2] ^= table[source[i + 2]];
            destination[i + 3] ^= table[source[i + 3]];
            destination[i + 4] ^= table[source[i + 4]];
            destination[i + 5] ^= table[source[i + 5]];
            destination[i + 6] ^= table[source[i + 6]];
            destination[i + 7] ^= table[source[i + 7]];
        }

        for (; i < length; i++)
        {
            destination[i] ^= table[source[i]];
        }
    }
}

internal static class GaloisField2
{
    internal const int Size = 256;

    private const int Mask = Size - 1;
    private const int GeneratorPolynomial = 301;

    private const int MultiplyTableLength = Size * Size;
    private const int InverseOffset = MultiplyTableLength;

    internal static readonly byte[] Tables = CreateTables();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte Multiply(
        byte a,
        byte b)
        => Tables[(a << 8) | b];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte Inverse(byte value)
        => Tables[InverseOffset + value];

    private static byte[] CreateTables()
    {
        var tables =
            new byte[MultiplyTableLength + Size];

        Span<byte> log =
            stackalloc byte[Size];

        Span<byte> exp =
            stackalloc byte[Size * 2];

        var value = 1;

        for (var exponent = 0;
            exponent < Mask;
            exponent++)
        {
            exp[exponent] = (byte)value;
            log[value] = (byte)exponent;

            value <<= 1;

            if (value >= Size)
            {
                value =
                    (value ^ GeneratorPolynomial) &
                    Mask;
            }
        }

        for (var exponent = Mask;
            exponent < exp.Length;
            exponent++)
        {
            exp[exponent] =
                exp[exponent - Mask];
        }

        for (var a = 1;
            a < Size;
            a++)
        {
            var logA = log[a];
            var row = a << 8;

            for (var b = 1;
                b < Size;
                b++)
            {
                tables[row | b] =
                    exp[logA + log[b]];
            }

            tables[InverseOffset + a] =
                exp[Mask - logA];
        }

        return tables;
    }
}
