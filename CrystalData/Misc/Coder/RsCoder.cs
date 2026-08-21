// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

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
/// The encoded buffer contains all data shards followed by all parity shards.
/// This type is thread-safe after construction.
/// </remarks>
public sealed class RsCoder
{
    /// <summary>
    /// The default number of data shards.
    /// </summary>
    public const int DefaultDataShardCount = 8;

    /// <summary>
    /// The default number of parity shards.
    /// </summary>
    public const int DefaultParityShardCount = 4;

    private const int StackallocThreshold = 4096;

    // Shards are processed in cache-sized blocks so that a destination block stays
    // in L1 while every data shard contributes to it, and the source blocks of all
    // data shards stay in L1/L2 across the parity rows.
    private const int BlockSize = 4096;

    // Generator matrix rows used to produce parity shards.
    private readonly byte[] parityRows;

    /// <summary>
    /// Initializes a new instance of the <see cref="RsCoder"/> class.
    /// </summary>
    /// <param name="dataShardCount">The number of data shards.</param>
    /// <param name="parityShardCount">The number of parity shards.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The shard counts are invalid or their total exceeds 256.
    /// </exception>
    public RsCoder(int dataShardCount = DefaultDataShardCount, int parityShardCount = DefaultParityShardCount)
    {
        if (dataShardCount is < 1 or > GaloisField.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(dataShardCount));
        }

        if (parityShardCount is < 1 or > GaloisField.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(parityShardCount));
        }

        var shardCount = dataShardCount + parityShardCount;
        if (shardCount > GaloisField.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(parityShardCount), "The total number of shards must not exceed 256.");
        }

        this.DataShardCount = dataShardCount;
        this.ParityShardCount = parityShardCount;
        this.ShardCount = shardCount;
        this.parityRows = GC.AllocateUninitializedArray<byte>(dataShardCount * parityShardCount);
        this.GenerateParityRows();
    }

    /// <summary>
    /// Gets the number of data shards required to reconstruct the original data.
    /// </summary>
    public int DataShardCount { get; }

    /// <summary>
    /// Gets the number of parity shards.
    /// </summary>
    public int ParityShardCount { get; }

    /// <summary>
    /// Gets the total number of data and parity shards.
    /// </summary>
    public int ShardCount { get; }

    /// <summary>
    /// Gets the length of each shard for the specified data length.
    /// </summary>
    /// <param name="dataLength">The original data length in bytes.</param>
    /// <returns>The length of each shard in bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dataLength"/> is negative.
    /// </exception>
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
    /// <param name="dataLength">The original data length in bytes.</param>
    /// <returns>The required encoded buffer length in bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dataLength"/> is negative.
    /// </exception>
    /// <exception cref="OverflowException">
    /// The resulting encoded length exceeds <see cref="int.MaxValue"/>.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetEncodedLength(int dataLength) => checked(this.GetShardLength(dataLength) * this.ShardCount);

    /// <summary>
    /// Encodes the specified data into systematic Reed-Solomon shards.
    /// </summary>
    /// <param name="source">The source data to encode.</param>
    /// <returns>A buffer containing all data shards followed by all parity shards.</returns>
    public byte[] Encode(ReadOnlySpan<byte> source)
    {
        var encoded = GC.AllocateUninitializedArray<byte>(this.GetEncodedLength(source.Length));
        this.Encode(source, encoded);
        return encoded;
    }

    /// <summary>
    /// Encodes the specified data into systematic Reed-Solomon shards.
    /// </summary>
    /// <param name="source">The source data to encode.</param>
    /// <param name="destination">
    /// The destination buffer that receives all data shards followed by all parity shards.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is too small.
    /// </exception>
    public unsafe void Encode(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var shardLength = this.GetShardLength(source.Length);
        var requiredLength = checked(shardLength * this.ShardCount);
        if (destination.Length < requiredLength)
        {
            throw new ArgumentException("The destination buffer is too small.", nameof(destination));
        }

        if (source.IsEmpty)
        {
            return;
        }

        var n = this.DataShardCount;
        var m = this.ParityShardCount;
        var dataLength = shardLength * n;
        destination = destination[..requiredLength];

        // Store the original data as systematic data shards and zero the padding.
        source.CopyTo(destination);
        destination.Slice(source.Length, dataLength - source.Length).Clear();

        fixed (byte* pEncoded = destination)
        fixed (byte* pParity = this.parityRows)
        fixed (byte* pMultiply = GaloisField.Tables)
        fixed (byte* pNibbles = GaloisField.NibbleTables)
        {
            // parity = sum(data[i] * coefficient[i]) over GF(256), block by block
            // so that each parity block stays cache-resident across all data shards.
            for (var blockStart = 0; blockStart < shardLength; blockStart += BlockSize)
            {
                var blockLength = Math.Min(BlockSize, shardLength - blockStart);
                for (var parityIndex = 0; parityIndex < m; parityIndex++)
                {
                    var destinationBlock = pEncoded + ((n + parityIndex) * shardLength) + blockStart;
                    var coefficients = pParity + (parityIndex * n);
                    var initialized = false;
                    for (var dataIndex = 0; dataIndex < n; dataIndex++)
                    {
                        var coefficient = coefficients[dataIndex];
                        if (coefficient == 0)
                        {
                            continue;
                        }

                        var sourceBlock = pEncoded + (dataIndex * shardLength) + blockStart;
                        if (!initialized)
                        {
                            MultiplyCopy(destinationBlock, sourceBlock, blockLength, coefficient, pMultiply, pNibbles);
                            initialized = true;
                        }
                        else
                        {
                            XorMultiply(destinationBlock, sourceBlock, blockLength, coefficient, pMultiply, pNibbles);
                        }
                    }

                    if (!initialized)
                    {
                        new Span<byte>(destinationBlock, blockLength).Clear();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Recovers the original data from the available shards.
    /// </summary>
    /// <param name="shards">The encoded buffer containing all data and parity shard positions.</param>
    /// <param name="shardAvailable">
    /// Indicates which shards are available. The first <see cref="DataShardCount"/> entries
    /// correspond to data shards and the remaining entries correspond to parity shards.
    /// </param>
    /// <param name="dataLength">The original data length in bytes.</param>
    /// <returns>The recovered original data.</returns>
    /// <exception cref="ArgumentException">
    /// One of the supplied buffers is too small.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dataLength"/> is negative.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Fewer than <see cref="DataShardCount"/> shards are available.
    /// </exception>
    public byte[] Decode(ReadOnlySpan<byte> shards, ReadOnlySpan<bool> shardAvailable, int dataLength)
    {
        if (dataLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dataLength));
        }

        var destination = GC.AllocateUninitializedArray<byte>(dataLength);
        this.Decode(shards, shardAvailable, dataLength, destination);
        return destination;
    }

    /// <summary>
    /// Recovers the original data from the available shards.
    /// </summary>
    /// <param name="shards">The encoded buffer containing all data and parity shard positions.</param>
    /// <param name="shardAvailable">Indicates which shards are available.</param>
    /// <param name="dataLength">The original data length in bytes.</param>
    /// <param name="destination">The buffer that receives the recovered data.</param>
    /// <exception cref="ArgumentException">
    /// One of the supplied buffers is too small or overlaps an unsupported region.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dataLength"/> is negative.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Fewer than <see cref="DataShardCount"/> shards are available.
    /// </exception>
    public void Decode(ReadOnlySpan<byte> shards, ReadOnlySpan<bool> shardAvailable, int dataLength, Span<byte> destination)
    {
        if (!this.TryDecode(shards, shardAvailable, dataLength, destination))
        {
            throw new InvalidDataException("There are not enough shards to recover the original data.");
        }
    }

    /// <summary>
    /// Attempts to recover the original data from the available shards.
    /// </summary>
    /// <param name="shards">The encoded buffer containing all data and parity shard positions.</param>
    /// <param name="shardAvailable">Indicates which shards are available.</param>
    /// <param name="dataLength">The original data length in bytes.</param>
    /// <param name="destination">The buffer that receives the recovered data.</param>
    /// <returns>
    /// <see langword="true"/> if the data was recovered; otherwise, <see langword="false"/>
    /// if fewer than <see cref="DataShardCount"/> shards are available.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// One of the supplied buffers is too small or overlaps an unsupported region.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dataLength"/> is negative.
    /// </exception>
    public unsafe bool TryDecode(ReadOnlySpan<byte> shards, ReadOnlySpan<bool> shardAvailable, int dataLength, Span<byte> destination)
    {
        if (dataLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dataLength));
        }

        if (shardAvailable.Length < this.ShardCount)
        {
            throw new ArgumentException("The shard availability span is too small.", nameof(shardAvailable));
        }

        if (destination.Length < dataLength)
        {
            throw new ArgumentException("The destination buffer is too small.", nameof(destination));
        }

        var shardLength = this.GetShardLength(dataLength);
        var requiredLength = checked(shardLength * this.ShardCount);
        if (shards.Length < requiredLength)
        {
            throw new ArgumentException("The encoded shard buffer is too small.", nameof(shards));
        }

        if (dataLength == 0)
        {
            return true;
        }

        destination = destination[..dataLength];
        shards = shards[..requiredLength];

        // Exact in-place decoding is allowed, but partial overlap is not.
        if (shards.Overlaps(destination, out var overlapOffset) && overlapOffset != 0)
        {
            throw new ArgumentException("The destination must not partially overlap the encoded buffer.", nameof(destination));
        }

        var dataCount = this.DataShardCount;
        var usedDataShards = (dataLength / shardLength) + ((dataLength % shardLength) == 0 ? 0 : 1);
        var allRequiredDataAvailable = true;
        for (var i = 0; i < usedDataShards; i++)
        {
            if (!shardAvailable[i])
            {
                allRequiredDataAvailable = false;
                break;
            }
        }

        // Fast path: systematic data shards can be copied directly.
        if (allRequiredDataAvailable)
        {
            shards[..dataLength].CopyTo(destination);
            return true;
        }

        var availableCount = 0;
        for (var i = 0; i < this.ShardCount && availableCount < dataCount; i++)
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

        var matrixWidth = dataCount << 1;
        var matrixLength = checked(dataCount * matrixWidth);
        var scratchLength = checked(matrixLength + dataCount);
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

            // Build [selected generator rows | identity].
            var selectedCount = 0;
            for (var shardIndex = 0; shardIndex < this.ShardCount && selectedCount < dataCount; shardIndex++)
            {
                if (!shardAvailable[shardIndex])
                {
                    continue;
                }

                selected[selectedCount] = (byte)shardIndex;
                var row = matrix.Slice(selectedCount * matrixWidth, matrixWidth);
                if (shardIndex < dataCount)
                {
                    row[shardIndex] = 1;
                }
                else
                {
                    this.parityRows.AsSpan((shardIndex - dataCount) * dataCount, dataCount).CopyTo(row);
                }

                row[dataCount + selectedCount] = 1;
                selectedCount++;
            }

            // The right half becomes the decoding matrix.
            InvertAugmentedMatrix(matrix, dataCount);

            fixed (byte* pShards = shards)
            fixed (byte* pDestination = destination)
            fixed (byte* pMatrix = matrix)
            fixed (byte* pSelected = selected)
            fixed (byte* pMultiply = GaloisField.Tables)
            fixed (byte* pNibbles = GaloisField.NibbleTables)
            {
                for (var dataIndex = 0; dataIndex < usedDataShards; dataIndex++)
                {
                    var outputOffset = dataIndex * shardLength;
                    var outputLength = Math.Min(shardLength, dataLength - outputOffset);
                    var output = pDestination + outputOffset;

                    // Copy an available systematic shard directly.
                    if (shardAvailable[dataIndex])
                    {
                        var source = pShards + outputOffset;
                        new ReadOnlySpan<byte>(source, outputLength).CopyTo(new Span<byte>(output, outputLength));
                        continue;
                    }

                    // Reconstruct a missing data shard from the selected shards.
                    var inverseRow = pMatrix + (dataIndex * matrixWidth) + dataCount;
                    var initialized = false;
                    for (var selectedIndex = 0; selectedIndex < dataCount; selectedIndex++)
                    {
                        var coefficient = inverseRow[selectedIndex];
                        if (coefficient == 0)
                        {
                            continue;
                        }

                        var source = pShards + (pSelected[selectedIndex] * shardLength);
                        if (!initialized)
                        {
                            MultiplyCopy(output, source, outputLength, coefficient, pMultiply, pNibbles);
                            initialized = true;
                        }
                        else
                        {
                            XorMultiply(output, source, outputLength, coefficient, pMultiply, pNibbles);
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
    /// Attempts to recover the original data into the beginning of the encoded buffer.
    /// </summary>
    /// <param name="shards">
    /// The encoded shard buffer. Its beginning is overwritten with the recovered data.
    /// </param>
    /// <param name="shardAvailable">Indicates which shards are available.</param>
    /// <param name="dataLength">The original data length in bytes.</param>
    /// <returns>
    /// <see langword="true"/> if the data was recovered; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="shards"/> or <paramref name="shardAvailable"/> is too small.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dataLength"/> is negative.
    /// </exception>
    public bool TryDecodeInPlace(Span<byte> shards, ReadOnlySpan<bool> shardAvailable, int dataLength)
    {
        if (dataLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dataLength));
        }

        if (shards.Length < dataLength)
        {
            throw new ArgumentException("The shard buffer is too small.", nameof(shards));
        }

        return this.TryDecode(shards, shardAvailable, dataLength, shards[..dataLength]);
    }

    /// <summary>
    /// Recovers the original data into the beginning of the encoded buffer.
    /// </summary>
    /// <param name="shards">
    /// The encoded shard buffer. Its beginning is overwritten with the recovered data.
    /// </param>
    /// <param name="shardAvailable">Indicates which shards are available.</param>
    /// <param name="dataLength">The original data length in bytes.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="shards"/> or <paramref name="shardAvailable"/> is too small.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dataLength"/> is negative.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Fewer than <see cref="DataShardCount"/> shards are available.
    /// </exception>
    public void DecodeInPlace(Span<byte> shards, ReadOnlySpan<bool> shardAvailable, int dataLength)
    {
        if (!this.TryDecodeInPlace(shards, shardAvailable, dataLength))
        {
            throw new InvalidDataException("There are not enough shards to recover the original data.");
        }
    }

    /// <summary>
    /// Gets a read-only view of the specified shard.
    /// </summary>
    /// <param name="encoded">The encoded buffer.</param>
    /// <param name="dataLength">The original data length in bytes.</param>
    /// <param name="shardIndex">The zero-based shard index.</param>
    /// <returns>A read-only span representing the specified shard.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dataLength"/> or <paramref name="shardIndex"/> is invalid.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="encoded"/> is too small.
    /// </exception>
    public ReadOnlySpan<byte> GetShard(ReadOnlySpan<byte> encoded, int dataLength, int shardIndex)
    {
        var shardLength = this.ValidateShardAccess(encoded.Length, dataLength, shardIndex);
        return encoded.Slice(shardIndex * shardLength, shardLength);
    }

    /// <summary>
    /// Gets a writable view of the specified shard.
    /// </summary>
    /// <param name="encoded">The encoded buffer.</param>
    /// <param name="dataLength">The original data length in bytes.</param>
    /// <param name="shardIndex">The zero-based shard index.</param>
    /// <returns>A writable span representing the specified shard.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dataLength"/> or <paramref name="shardIndex"/> is invalid.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="encoded"/> is too small.
    /// </exception>
    public Span<byte> GetShard(Span<byte> encoded, int dataLength, int shardIndex)
    {
        var shardLength = this.ValidateShardAccess(encoded.Length, dataLength, shardIndex);
        return encoded.Slice(shardIndex * shardLength, shardLength);
    }

    /// <summary>
    /// Returns a string describing the Reed-Solomon configuration.
    /// </summary>
    /// <returns>A string containing the data and parity shard counts.</returns>
    public override string ToString() => $"RsCoder Data: {this.DataShardCount}, Parity: {this.ParityShardCount}";

    private int ValidateShardAccess(int encodedLength, int dataLength, int shardIndex)
    {
        if ((uint)shardIndex >= (uint)this.ShardCount)
        {
            throw new ArgumentOutOfRangeException(nameof(shardIndex));
        }

        var shardLength = this.GetShardLength(dataLength);
        var requiredLength = checked(shardLength * this.ShardCount);
        if (encodedLength < requiredLength)
        {
            throw new ArgumentException("The encoded buffer is too small.", nameof(encodedLength));
        }

        return shardLength;
    }

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
            rented = ArrayPool<byte>.Shared.Rent(scratchLength);
            scratch = rented.AsSpan(0, scratchLength);
        }

        try
        {
            var matrix = scratch[..matrixLength];
            var vandermonde = scratch.Slice(matrixLength, n);
            matrix.Clear();

            // Build and invert the leading n x n Vandermonde matrix.
            for (var rowIndex = 0; rowIndex < n; rowIndex++)
            {
                var row = matrix.Slice(rowIndex * matrixWidth, matrixWidth);
                FillVandermondeRow(rowIndex, row[..n]);
                row[n + rowIndex] = 1;
            }

            InvertAugmentedMatrix(matrix, n);

            // Convert the Vandermonde matrix into a systematic generator matrix.
            for (var parityIndex = 0; parityIndex < this.ParityShardCount; parityIndex++)
            {
                FillVandermondeRow(n + parityIndex, vandermonde);
                var destination = this.parityRows.AsSpan(parityIndex * n, n);
                for (var column = 0; column < n; column++)
                {
                    byte value = 0;
                    for (var k = 0; k < n; k++)
                    {
                        value ^= GaloisField.Multiply(vandermonde[k], matrix[(k * matrixWidth) + n + column]);
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
    private static void FillVandermondeRow(int x, Span<byte> row)
    {
        // Row = [1, x, x^2, ...] over GF(256).
        row[0] = 1;
        if (row.Length == 1)
        {
            return;
        }

        var fieldX = (byte)x;
        byte value = 1;
        for (var column = 1; column < row.Length; column++)
        {
            value = GaloisField.Multiply(value, fieldX);
            row[column] = value;
        }
    }

    private static void InvertAugmentedMatrix(Span<byte> matrix, int n)
    {
        var width = n << 1;

        // Gauss-Jordan elimination over GF(256).
        for (var column = 0; column < n; column++)
        {
            var pivotRow = column;
            while (pivotRow < n && matrix[(pivotRow * width) + column] == 0)
            {
                pivotRow++;
            }

            if (pivotRow == n)
            {
                throw new InvalidOperationException("The Reed-Solomon matrix is singular.");
            }

            var currentOffset = column * width;
            if (pivotRow != column)
            {
                var pivotOffset = pivotRow * width;
                for (var x = column; x < width; x++)
                {
                    var temp = matrix[currentOffset + x];
                    matrix[currentOffset + x] = matrix[pivotOffset + x];
                    matrix[pivotOffset + x] = temp;
                }
            }

            // Normalize the pivot row.
            var pivot = matrix[currentOffset + column];
            if (pivot != 1)
            {
                var inverse = GaloisField.Inverse(pivot);
                matrix[currentOffset + column] = 1;
                for (var x = column + 1; x < width; x++)
                {
                    matrix[currentOffset + x] = GaloisField.Multiply(inverse, matrix[currentOffset + x]);
                }
            }

            // Eliminate this column from all other rows.
            for (var row = 0; row < n; row++)
            {
                if (row == column)
                {
                    continue;
                }

                var rowOffset = row * width;
                var factor = matrix[rowOffset + column];
                if (factor == 0)
                {
                    continue;
                }

                matrix[rowOffset + column] = 0;
                for (var x = column + 1; x < width; x++)
                {
                    matrix[rowOffset + x] ^= GaloisField.Multiply(factor, matrix[currentOffset + x]);
                }
            }
        }
    }

    // destination = source * coefficient over GF(256).
    private static unsafe void MultiplyCopy(byte* destination, byte* source, int length, byte coefficient, byte* multiply, byte* nibbles)
    {
        // Multiplication by one is a plain copy.
        if (coefficient == 1)
        {
            Buffer.MemoryCopy(source, destination, length, length);
            return;
        }

        var i = 0;
        if (Avx2.IsSupported)
        {
            if (length >= 32)
            {
                var tableLo = Avx2.BroadcastVector128ToVector256(nibbles + ((nuint)coefficient << 5));
                var tableHi = Avx2.BroadcastVector128ToVector256(nibbles + ((nuint)coefficient << 5) + 16);
                var mask = Vector256.Create((byte)0x0F);
                for (; i <= length - 64; i += 64)
                {
                    var r0 = MultiplyVector(Avx.LoadVector256(source + i), tableLo, tableHi, mask);
                    var r1 = MultiplyVector(Avx.LoadVector256(source + i + 32), tableLo, tableHi, mask);
                    Avx.Store(destination + i, r0);
                    Avx.Store(destination + i + 32, r1);
                }

                for (; i <= length - 32; i += 32)
                {
                    Avx.Store(destination + i, MultiplyVector(Avx.LoadVector256(source + i), tableLo, tableHi, mask));
                }
            }
        }
        else if (Ssse3.IsSupported || AdvSimd.Arm64.IsSupported)
        {
            if (length >= 16)
            {
                var tableLo = Vector128.Load(nibbles + ((nuint)coefficient << 5));
                var tableHi = Vector128.Load(nibbles + ((nuint)coefficient << 5) + 16);
                var mask = Vector128.Create((byte)0x0F);
                for (; i <= length - 32; i += 32)
                {
                    var r0 = MultiplyVector(Vector128.Load(source + i), tableLo, tableHi, mask);
                    var r1 = MultiplyVector(Vector128.Load(source + i + 16), tableLo, tableHi, mask);
                    r0.Store(destination + i);
                    r1.Store(destination + i + 16);
                }

                for (; i <= length - 16; i += 16)
                {
                    MultiplyVector(Vector128.Load(source + i), tableLo, tableHi, mask).Store(destination + i);
                }
            }
        }

        // Scalar path: remaining tail bytes, or all bytes without SIMD support.
        var table = multiply + ((nuint)coefficient << 8);
        for (; i <= length - 8; i += 8)
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

    // destination ^= source * coefficient over GF(256).
    private static unsafe void XorMultiply(byte* destination, byte* source, int length, byte coefficient, byte* multiply, byte* nibbles)
    {
        var i = 0;

        // Multiplication by one reduces to XOR.
        if (coefficient == 1)
        {
            if (Vector256.IsHardwareAccelerated)
            {
                for (; i <= length - 32; i += 32)
                {
                    Vector256.Store(Vector256.Load(destination + i) ^ Vector256.Load(source + i), destination + i);
                }
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                for (; i <= length - 16; i += 16)
                {
                    Vector128.Store(Vector128.Load(destination + i) ^ Vector128.Load(source + i), destination + i);
                }
            }

            for (; i <= length - sizeof(nuint); i += sizeof(nuint))
            {
                var value = Unsafe.ReadUnaligned<nuint>(destination + i) ^ Unsafe.ReadUnaligned<nuint>(source + i);
                Unsafe.WriteUnaligned(destination + i, value);
            }

            for (; i < length; i++)
            {
                destination[i] ^= source[i];
            }

            return;
        }

        if (Avx2.IsSupported)
        {
            if (length >= 32)
            {
                var tableLo = Avx2.BroadcastVector128ToVector256(nibbles + ((nuint)coefficient << 5));
                var tableHi = Avx2.BroadcastVector128ToVector256(nibbles + ((nuint)coefficient << 5) + 16);
                var mask = Vector256.Create((byte)0x0F);
                for (; i <= length - 64; i += 64)
                {
                    var r0 = MultiplyVector(Avx.LoadVector256(source + i), tableLo, tableHi, mask);
                    var r1 = MultiplyVector(Avx.LoadVector256(source + i + 32), tableLo, tableHi, mask);
                    Avx.Store(destination + i, Avx2.Xor(Avx.LoadVector256(destination + i), r0));
                    Avx.Store(destination + i + 32, Avx2.Xor(Avx.LoadVector256(destination + i + 32), r1));
                }

                for (; i <= length - 32; i += 32)
                {
                    var r = MultiplyVector(Avx.LoadVector256(source + i), tableLo, tableHi, mask);
                    Avx.Store(destination + i, Avx2.Xor(Avx.LoadVector256(destination + i), r));
                }
            }
        }
        else if (Ssse3.IsSupported || AdvSimd.Arm64.IsSupported)
        {
            if (length >= 16)
            {
                var tableLo = Vector128.Load(nibbles + ((nuint)coefficient << 5));
                var tableHi = Vector128.Load(nibbles + ((nuint)coefficient << 5) + 16);
                var mask = Vector128.Create((byte)0x0F);
                for (; i <= length - 32; i += 32)
                {
                    var r0 = MultiplyVector(Vector128.Load(source + i), tableLo, tableHi, mask);
                    var r1 = MultiplyVector(Vector128.Load(source + i + 16), tableLo, tableHi, mask);
                    (Vector128.Load(destination + i) ^ r0).Store(destination + i);
                    (Vector128.Load(destination + i + 16) ^ r1).Store(destination + i + 16);
                }

                for (; i <= length - 16; i += 16)
                {
                    var r = MultiplyVector(Vector128.Load(source + i), tableLo, tableHi, mask);
                    (Vector128.Load(destination + i) ^ r).Store(destination + i);
                }
            }
        }

        // Scalar path: remaining tail bytes, or all bytes without SIMD support.
        var table = multiply + ((nuint)coefficient << 8);
        for (; i <= length - 8; i += 8)
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

    // Multiplies 32 bytes by a fixed coefficient using two nibble lookup tables:
    // product = tableLo[b & 0x0F] ^ tableHi[b >> 4].
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<byte> MultiplyVector(Vector256<byte> value, Vector256<byte> tableLo, Vector256<byte> tableHi, Vector256<byte> mask)
        => Avx2.Xor(
            Avx2.Shuffle(tableLo, Avx2.And(value, mask)),
            Avx2.Shuffle(tableHi, Avx2.And(Avx2.ShiftRightLogical(value.AsUInt64(), 4).AsByte(), mask)));

    // Multiplies 16 bytes by a fixed coefficient using two nibble lookup tables.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> MultiplyVector(Vector128<byte> value, Vector128<byte> tableLo, Vector128<byte> tableHi, Vector128<byte> mask)
    {
        if (Ssse3.IsSupported)
        {
            return Sse2.Xor(
                Ssse3.Shuffle(tableLo, Sse2.And(value, mask)),
                Ssse3.Shuffle(tableHi, Sse2.And(Sse2.ShiftRightLogical(value.AsUInt64(), 4).AsByte(), mask)));
        }

        return AdvSimd.Xor(
            AdvSimd.Arm64.VectorTableLookup(tableLo, AdvSimd.And(value, mask)),
            AdvSimd.Arm64.VectorTableLookup(tableHi, AdvSimd.ShiftRightLogical(value, 4)));
    }
}

internal static class GaloisField
{
    internal const int Size = 256;

    private const int Mask = Size - 1;
    private const int GeneratorPolynomial = 301;
    private const int MultiplyTableLength = Size * Size;
    private const int InverseOffset = MultiplyTableLength;

    // Layout: 256 multiplication rows followed by 256 multiplicative inverses.
    internal static readonly byte[] Tables = CreateTables();

    // Per-coefficient 16-entry nibble tables for SIMD shuffle multiplication.
    // Layout per coefficient c (32 bytes): [c * 0x00..0x0F] then [c * 0x00, c * 0x10, ..., c * 0xF0].
    internal static readonly byte[] NibbleTables = CreateNibbleTables();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte Multiply(byte a, byte b) => Tables[(a << 8) | b];

    // The caller must pass a non-zero value.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte Inverse(byte value) => Tables[InverseOffset + value];

    private static byte[] CreateTables()
    {
        var tables = new byte[MultiplyTableLength + Size];
        Span<byte> log = stackalloc byte[Size];
        Span<byte> exp = stackalloc byte[Size * 2];

        // Build logarithm and exponent tables for GF(256).
        var value = 1;
        for (var exponent = 0; exponent < Mask; exponent++)
        {
            exp[exponent] = (byte)value;
            log[value] = (byte)exponent;
            value <<= 1;
            if (value >= Size)
            {
                value = (value ^ GeneratorPolynomial) & Mask;
            }
        }

        // Duplicate the exponent table to avoid modulo 255 in hot paths.
        for (var exponent = Mask; exponent < exp.Length; exponent++)
        {
            exp[exponent] = exp[exponent - Mask];
        }

        // Precompute multiplication and inverse tables.
        for (var a = 1; a < Size; a++)
        {
            var logA = log[a];
            var row = a << 8;
            for (var b = 1; b < Size; b++)
            {
                tables[row | b] = exp[logA + log[b]];
            }

            tables[InverseOffset + a] = exp[Mask - logA];
        }

        return tables;
    }

    private static byte[] CreateNibbleTables()
    {
        var tables = new byte[Size * 32];
        for (var c = 0; c < Size; c++)
        {
            var offset = c << 5;
            for (var i = 0; i < 16; i++)
            {
                tables[offset + i] = Multiply((byte)c, (byte)i);
                tables[offset + 16 + i] = Multiply((byte)c, (byte)(i << 4));
            }
        }

        return tables;
    }
}
