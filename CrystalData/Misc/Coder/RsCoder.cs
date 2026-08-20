// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

#pragma warning disable SA1519 // Braces should not be omitted from multi-line child statement

namespace CrystalData;

public class GaloisField
{
    public const int Max = 256;
    public const int Mask = Max - 1;
    public const int FieldGenPoly = 301; // 301 > 285 > 435

    private static readonly GaloisField DefaultField = new(FieldGenPoly);
    private static readonly Dictionary<int, GaloisField> FieldCache = new();
    private static readonly object FieldCacheLock = new();

    public static GaloisField Get(int fieldGenPoly)
    {
        if (fieldGenPoly == FieldGenPoly)
        {
            return DefaultField;
        }

        lock (FieldCacheLock)
        {
            if (!FieldCache.TryGetValue(fieldGenPoly, out var field))
            {
                field = new GaloisField(fieldGenPoly);
                FieldCache.Add(fieldGenPoly, field);
            }

            return field;
        }
    }

    private GaloisField(int fieldGenPoly)
    {
        var gf = new byte[Max];
        var gfi = new byte[Max];

        gf[0] = Mask;
        gfi[Mask] = 0;

        var value = 1;

        unchecked
        {
            for (var exponent = 0; exponent < Mask; exponent++)
            {
                gf[value] = (byte)exponent;
                gfi[exponent] = (byte)value;

                value <<= 1;
                if (value >= Max)
                {
                    value = (value ^ fieldGenPoly) & Mask;
                }
            }
        }

        this.GF = gf;
        this.GFI = gfi;

        var multi = new byte[Max * Max];
        var div = new byte[Max * Max];

        // Row/column zero is already initialized to zero.
        for (var a = 1; a < Max; a++)
        {
            var logA = gf[a];
            var row = a << 8;

            for (var b = 1; b < Max; b++)
            {
                var logB = gf[b];

                var productExponent = logA + logB;
                if (productExponent >= Mask)
                {
                    productExponent -= Mask;
                }

                multi[row | b] = gfi[productExponent];

                var quotientExponent = logA - logB;
                if (quotientExponent < 0)
                {
                    quotientExponent += Mask;
                }

                div[row | b] = gfi[quotientExponent];
            }
        }

        this.Multi = multi;
        this.Div = div;
    }

    public byte[] GF { get; }

    public byte[] GFI { get; }

    public byte[] Multi { get; }

    public byte[] Div { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte InternalMulti(int a, int b)
    {
        if (a == 0 || b == 0)
        {
            return 0;
        }

        var exponent = this.GF[a] + this.GF[b];
        if (exponent >= Mask)
        {
            exponent -= Mask;
        }

        return this.GFI[exponent];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte InternalDiv(int a, int b)
    {
        if (a == 0 || b == 0)
        {
            return 0;
        }

        var exponent = this.GF[a] - this.GF[b];
        if (exponent < 0)
        {
            exponent += Mask;
        }

        return this.GFI[exponent];
    }
}

public class RsCoder : IDisposable
{
    public const int DefaultDataSize = 8;
    public const int DefaultCheckSize = 4;

    /// <summary>
    /// Initializes a new instance of the <see cref="RsCoder"/> class (Reed-Solomon Coder.).
    /// </summary>
    /// <param name="dataSize">The Number of blocks of data to be split.</param>
    /// <param name="checkSize">The Number of blocks of checksum.</param>
    /// <param name="fieldGenPoly">Field generator polymoninal (default 301).</param>
    public RsCoder(
        int dataSize = DefaultDataSize,
        int checkSize = DefaultCheckSize,
        int fieldGenPoly = GaloisField.FieldGenPoly)
    {
        if (dataSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(dataSize));
        }

        if (checkSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(checkSize));
        }

        this.DataSize = dataSize;
        this.CheckSize = checkSize;
        this.TotalSize = dataSize + checkSize;

        if (this.TotalSize >= GaloisField.Max)
        {
            throw new ArgumentOutOfRangeException();
        }

        this.GaloisField = GaloisField.Get(fieldGenPoly);

        this.EnsureBuffers(false);
        this.GenerateEF();
    }

    public GaloisField GaloisField { get; }

    public int TotalSize { get; }

    public int DataSize { get; }

    public int CheckSize { get; }

    public byte[]? Source { get; set; }

    public byte[][]? EncodedBuffer => this.rentEncodeBuffer;

    public int EncodedBufferLength { get; set; }

    public byte[]? DecodedBuffer => this.rentDecodeBuffer;

    public int DecodedBufferLength { get; set; }

    public unsafe void Encode(byte[] source, int length)
    {
        ArgumentNullException.ThrowIfNull(source);

        if ((uint)length > (uint)source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var n = this.DataSize;

        if ((length % n) != 0)
        {
            throw new InvalidDataException(
                "Length of source data must be a multiple of RsCoder.DataSize.");
        }

        var m = this.CheckSize;
        var destinationLength = length / n;

        this.EncodedBufferLength = destinationLength;
        this.EnsureEncodeBuffer(destinationLength);

        var destination = this.rentEncodeBuffer!;
        var ef = this.rentEF!;
        var multi = this.GaloisField.Multi;

        // Most common configuration.
        if (n == 8 && m == 4)
        {
            fixed (byte* ps = source, pef = ef, pm = multi)
            fixed (
                byte* pd0 = destination[0],
                pd1 = destination[1],
                pd2 = destination[2],
                pd3 = destination[3],
                pd4 = destination[4],
                pd5 = destination[5],
                pd6 = destination[6],
                pd7 = destination[7],
                pc0 = destination[8],
                pc1 = destination[9],
                pc2 = destination[10],
                pc3 = destination[11])
            {
                var p = ps;

                var e0 = pef;
                var e1 = pef + 8;
                var e2 = pef + 16;
                var e3 = pef + 24;

                for (var x = 0; x < destinationLength; x++)
                {
                    var b0 = p[0];
                    var b1 = p[1];
                    var b2 = p[2];
                    var b3 = p[3];
                    var b4 = p[4];
                    var b5 = p[5];
                    var b6 = p[6];
                    var b7 = p[7];

                    pd0[x] = b0;
                    pd1[x] = b1;
                    pd2[x] = b2;
                    pd3[x] = b3;
                    pd4[x] = b4;
                    pd5[x] = b5;
                    pd6[x] = b6;
                    pd7[x] = b7;

                    pc0[x] = (byte)(
                        pm[(b0 << 8) | e0[0]] ^
                        pm[(b1 << 8) | e0[1]] ^
                        pm[(b2 << 8) | e0[2]] ^
                        pm[(b3 << 8) | e0[3]] ^
                        pm[(b4 << 8) | e0[4]] ^
                        pm[(b5 << 8) | e0[5]] ^
                        pm[(b6 << 8) | e0[6]] ^
                        pm[(b7 << 8) | e0[7]]);

                    pc1[x] = (byte)(
                        pm[(b0 << 8) | e1[0]] ^
                        pm[(b1 << 8) | e1[1]] ^
                        pm[(b2 << 8) | e1[2]] ^
                        pm[(b3 << 8) | e1[3]] ^
                        pm[(b4 << 8) | e1[4]] ^
                        pm[(b5 << 8) | e1[5]] ^
                        pm[(b6 << 8) | e1[6]] ^
                        pm[(b7 << 8) | e1[7]]);

                    pc2[x] = (byte)(
                        pm[(b0 << 8) | e2[0]] ^
                        pm[(b1 << 8) | e2[1]] ^
                        pm[(b2 << 8) | e2[2]] ^
                        pm[(b3 << 8) | e2[3]] ^
                        pm[(b4 << 8) | e2[4]] ^
                        pm[(b5 << 8) | e2[5]] ^
                        pm[(b6 << 8) | e2[6]] ^
                        pm[(b7 << 8) | e2[7]]);

                    pc3[x] = (byte)(
                        pm[(b0 << 8) | e3[0]] ^
                        pm[(b1 << 8) | e3[1]] ^
                        pm[(b2 << 8) | e3[2]] ^
                        pm[(b3 << 8) | e3[3]] ^
                        pm[(b4 << 8) | e3[4]] ^
                        pm[(b5 << 8) | e3[5]] ^
                        pm[(b6 << 8) | e3[6]] ^
                        pm[(b7 << 8) | e3[7]]);

                    p += 8;
                }
            }

            return;
        }

        if (n == 4 && m == 4)
        {
            fixed (byte* ps = source, pef = ef, pm = multi)
            fixed (
                byte* pd0 = destination[0],
                pd1 = destination[1],
                pd2 = destination[2],
                pd3 = destination[3],
                pc0 = destination[4],
                pc1 = destination[5],
                pc2 = destination[6],
                pc3 = destination[7])
            {
                var p = ps;

                var e0 = pef;
                var e1 = pef + 4;
                var e2 = pef + 8;
                var e3 = pef + 12;

                for (var x = 0; x < destinationLength; x++)
                {
                    var b0 = p[0];
                    var b1 = p[1];
                    var b2 = p[2];
                    var b3 = p[3];

                    pd0[x] = b0;
                    pd1[x] = b1;
                    pd2[x] = b2;
                    pd3[x] = b3;

                    pc0[x] = (byte)(
                        pm[(b0 << 8) | e0[0]] ^
                        pm[(b1 << 8) | e0[1]] ^
                        pm[(b2 << 8) | e0[2]] ^
                        pm[(b3 << 8) | e0[3]]);

                    pc1[x] = (byte)(
                        pm[(b0 << 8) | e1[0]] ^
                        pm[(b1 << 8) | e1[1]] ^
                        pm[(b2 << 8) | e1[2]] ^
                        pm[(b3 << 8) | e1[3]]);

                    pc2[x] = (byte)(
                        pm[(b0 << 8) | e2[0]] ^
                        pm[(b1 << 8) | e2[1]] ^
                        pm[(b2 << 8) | e2[2]] ^
                        pm[(b3 << 8) | e2[3]]);

                    pc3[x] = (byte)(
                        pm[(b0 << 8) | e3[0]] ^
                        pm[(b1 << 8) | e3[1]] ^
                        pm[(b2 << 8) | e3[2]] ^
                        pm[(b3 << 8) | e3[3]]);

                    p += 4;
                }
            }

            return;
        }

        if (n == 8)
        {
            fixed (byte* ps = source, pef = ef, pm = multi)
            fixed (
                byte* pd0 = destination[0],
                pd1 = destination[1],
                pd2 = destination[2],
                pd3 = destination[3],
                pd4 = destination[4],
                pd5 = destination[5],
                pd6 = destination[6],
                pd7 = destination[7])
            {
                var p = ps;

                for (var x = 0; x < destinationLength; x++)
                {
                    var b0 = p[0];
                    var b1 = p[1];
                    var b2 = p[2];
                    var b3 = p[3];
                    var b4 = p[4];
                    var b5 = p[5];
                    var b6 = p[6];
                    var b7 = p[7];

                    pd0[x] = b0;
                    pd1[x] = b1;
                    pd2[x] = b2;
                    pd3[x] = b3;
                    pd4[x] = b4;
                    pd5[x] = b5;
                    pd6[x] = b6;
                    pd7[x] = b7;

                    for (var y = 0; y < m; y++)
                    {
                        var e = pef + (y << 3);

                        destination[8 + y][x] = (byte)(
                            pm[(b0 << 8) | e[0]] ^
                            pm[(b1 << 8) | e[1]] ^
                            pm[(b2 << 8) | e[2]] ^
                            pm[(b3 << 8) | e[3]] ^
                            pm[(b4 << 8) | e[4]] ^
                            pm[(b5 << 8) | e[5]] ^
                            pm[(b6 << 8) | e[6]] ^
                            pm[(b7 << 8) | e[7]]);
                    }

                    p += 8;
                }
            }

            return;
        }

        if (n == 4)
        {
            fixed (byte* ps = source, pef = ef, pm = multi)
            fixed (
                byte* pd0 = destination[0],
                pd1 = destination[1],
                pd2 = destination[2],
                pd3 = destination[3])
            {
                var p = ps;

                for (var x = 0; x < destinationLength; x++)
                {
                    var b0 = p[0];
                    var b1 = p[1];
                    var b2 = p[2];
                    var b3 = p[3];

                    pd0[x] = b0;
                    pd1[x] = b1;
                    pd2[x] = b2;
                    pd3[x] = b3;

                    for (var y = 0; y < m; y++)
                    {
                        var e = pef + (y << 2);

                        destination[4 + y][x] = (byte)(
                            pm[(b0 << 8) | e[0]] ^
                            pm[(b1 << 8) | e[1]] ^
                            pm[(b2 << 8) | e[2]] ^
                            pm[(b3 << 8) | e[3]]);
                    }

                    p += 4;
                }
            }

            return;
        }

        fixed (byte* ps = source, pef = ef, pm = multi)
        {
            var p = ps;

            for (var x = 0; x < destinationLength; x++)
            {
                for (var y = 0; y < n; y++)
                {
                    destination[y][x] = p[y];
                }

                for (var y = 0; y < m; y++)
                {
                    var e = pef + (y * n);
                    var result = 0;

                    for (var z = 0; z < n; z++)
                    {
                        result ^= pm[(p[z] << 8) | e[z]];
                    }

                    destination[n + y][x] = (byte)result;
                }

                p += n;
            }
        }
    }

    public unsafe void Decode(byte[]?[] source, int length)
    {
        ArgumentNullException.ThrowIfNull(source);

        var n = this.DataSize;
        var m = this.CheckSize;
        var nm = this.TotalSize;

        if (source.Length < nm)
        {
            throw new InvalidDataException(
                "The number of source byte arrays is insufficient.");
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var validCount = 0;

        for (var i = 0; i < nm; i++)
        {
            var block = source[i];

            if (block is null)
            {
                continue;
            }

            if (block.Length < length)
            {
                throw new InvalidDataException(
                    "Length of source byte arrays must be greater than or equal to 'length'.");
            }

            validCount++;
        }

        if (validCount < n)
        {
            throw new InvalidDataException(
                "Number of valid byte arrays must be greater than or equal to RsCoder.DataSize.");
        }

        this.DecodedBufferLength = length * n;
        this.EnsureDecodeBuffer(this.DecodedBufferLength);

        if (length == 0)
        {
            return;
        }

        var destination = this.rentDecodeBuffer!;
        var multi = this.GaloisField.Multi;

        this.EnsureBuffers(true);

        var ef = this.rentEF!;
        var el = this.rentEL!;
        var er = this.rentER!;
        var s = this.rentS!;

        var matrixWidth = n << 1;
        el.AsSpan(0, n * matrixWidth).Clear();

        var checkIndex = 0;

        for (var x = 0; x < n; x++)
        {
            byte[] selected;
            int sourceIndex;

            var data = source[x];

            if (data is not null)
            {
                selected = data;
                sourceIndex = x;
            }
            else
            {
                while (checkIndex < m && source[n + checkIndex] is null)
                {
                    checkIndex++;
                }

                if (checkIndex >= m)
                {
                    throw new InvalidDataException(
                        "Number of valid byte arrays must be greater than or equal to RsCoder.DataSize.");
                }

                sourceIndex = n + checkIndex;
                selected = source[sourceIndex]!;
                checkIndex++;
            }

            var row = x * matrixWidth;

            if (sourceIndex < n)
            {
                el[row + sourceIndex] = 1;
            }
            else
            {
                ef.AsSpan((sourceIndex - n) * n, n)
                    .CopyTo(el.AsSpan(row, n));
            }

            el[row + n + x] = 1;
            s[x] = selected;
        }

        this.GenerateEL();

        for (var y = 0; y < n; y++)
        {
            el.AsSpan((y * matrixWidth) + n, n)
                .CopyTo(er.AsSpan(y * n, n));
        }

        if (n == 8)
        {
            fixed (byte* pm = multi, per = er, pd = destination)
            fixed (
                byte* ps0 = s[0],
                ps1 = s[1],
                ps2 = s[2],
                ps3 = s[3],
                ps4 = s[4],
                ps5 = s[5],
                ps6 = s[6],
                ps7 = s[7])
            {
                var output = pd;

                for (var x = 0; x < length; x++)
                {
                    var b0 = ps0[x];
                    var b1 = ps1[x];
                    var b2 = ps2[x];
                    var b3 = ps3[x];
                    var b4 = ps4[x];
                    var b5 = ps5[x];
                    var b6 = ps6[x];
                    var b7 = ps7[x];

                    for (var y = 0; y < 8; y++)
                    {
                        var row = per + (y << 3);

                        var value =
                            pm[(row[0] << 8) | b0] ^
                            pm[(row[1] << 8) | b1] ^
                            pm[(row[2] << 8) | b2] ^
                            pm[(row[3] << 8) | b3] ^
                            pm[(row[4] << 8) | b4] ^
                            pm[(row[5] << 8) | b5] ^
                            pm[(row[6] << 8) | b6] ^
                            pm[(row[7] << 8) | b7];

                        *output++ = (byte)value;
                    }
                }
            }

            return;
        }

        if (n == 4)
        {
            fixed (byte* pm = multi, per = er, pd = destination)
            fixed (
                byte* ps0 = s[0],
                ps1 = s[1],
                ps2 = s[2],
                ps3 = s[3])
            {
                var output = pd;

                for (var x = 0; x < length; x++)
                {
                    var b0 = ps0[x];
                    var b1 = ps1[x];
                    var b2 = ps2[x];
                    var b3 = ps3[x];

                    for (var y = 0; y < 4; y++)
                    {
                        var row = per + (y << 2);

                        var value =
                            pm[(row[0] << 8) | b0] ^
                            pm[(row[1] << 8) | b1] ^
                            pm[(row[2] << 8) | b2] ^
                            pm[(row[3] << 8) | b3];

                        *output++ = (byte)value;
                    }
                }
            }

            return;
        }

        fixed (byte* pm = multi, per = er, pd = destination)
        {
            var output = pd;

            for (var x = 0; x < length; x++)
            {
                for (var y = 0; y < n; y++)
                {
                    var row = per + (y * n);
                    var value = 0;

                    for (var z = 0; z < n; z++)
                    {
                        value ^= pm[(row[z] << 8) | s[z][x]];
                    }

                    *output++ = (byte)value;
                }
            }
        }
    }

    public override string ToString()
        => $"RsCoder Data: {this.DataSize}, Check: {this.CheckSize}";

    public void InvalidateEncodedBufferForUnitTest(Random random, int number)
    {
        var buffers = this.rentEncodeBuffer;
        if (buffers is null)
        {
            return;
        }

        if (buffers.Length < number)
        {
            throw new InvalidOperationException();
        }

        var invalidNumber = 0;

        for (var i = 0; i < buffers.Length; i++)
        {
            if (buffers[i] is null)
            {
                invalidNumber++;
            }
        }

        var pool = ArrayPool<byte>.Shared;

        while (invalidNumber < number)
        {
            int i;

            do
            {
                i = random.Next(buffers.Length);
            }
            while (buffers[i] is null);

            pool.Return(buffers[i]);
            buffers[i] = null!;
            invalidNumber++;
        }
    }

    public void InvalidateEncodedBufferForUnitTest(uint bufferbits)
    {
        var buffers = this.rentEncodeBuffer;
        if (buffers is null)
        {
            return;
        }

        var pool = ArrayPool<byte>.Shared;

        for (var i = 0; i < buffers.Length; i++)
        {
            if ((bufferbits & (1u << i)) != 0)
            {
                continue;
            }

            var buffer = buffers[i];
            if (buffer is null)
            {
                continue;
            }

            pool.Return(buffer);
            buffers[i] = null!;
        }
    }

    public void TestReverseMatrix(uint sourceBits)
    {
        var n = this.DataSize;
        var m = this.CheckSize;

        this.EnsureBuffers(true);

        var ef = this.rentEF!;
        var el = this.rentEL!;

        var matrixWidth = n << 1;
        el.AsSpan(0, n * matrixWidth).Clear();

        var checkIndex = 0;

        for (var x = 0; x < n; x++)
        {
            int z;

            if (IsBitSet(sourceBits, x))
            {
                z = x;
            }
            else
            {
                while (checkIndex < m && !IsBitSet(sourceBits, n + checkIndex))
                {
                    checkIndex++;
                }

                if (checkIndex >= m)
                {
                    throw new InvalidDataException(
                        "The number of valid byte arrays must be greater than RsCoder.DataSize.");
                }

                z = n + checkIndex;
                checkIndex++;
            }

            var row = x * matrixWidth;

            if (z < n)
            {
                el[row + z] = 1;
            }
            else
            {
                ef.AsSpan((z - n) * n, n)
                    .CopyTo(el.AsSpan(row, n));
            }

            el[row + n + x] = 1;
        }

        this.GenerateEL();

        for (var y = 0; y < n; y++)
        {
            var row = y * matrixWidth;

            for (var x = 0; x < n; x++)
            {
                var expected = x == y ? 1 : 0;

                if (el[row + x] != expected)
                {
                    throw new Exception();
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBitSet(uint bits, int index)
        => (uint)index < 32 && (bits & (1u << index)) != 0;

    private void GenerateEF()
    {
        var n = this.DataSize;
        var m = this.CheckSize;

        var ef = this.rentEF!;
        var gfi = this.GaloisField.GFI;

        for (var y = 0; y < m; y++)
        {
            var row = y * n;
            var exponent = 0;

            for (var x = 0; x < n; x++)
            {
                ef[row + x] = gfi[exponent];

                exponent += y;
                if (exponent >= GaloisField.Mask)
                {
                    exponent -= GaloisField.Mask;
                }
            }
        }
    }

    private unsafe void GenerateEL()
    {
        var n = this.DataSize;
        var width = n << 1;

        var el = this.rentEL!;
        var multi = this.GaloisField.Multi;
        var div = this.GaloisField.Div;

        fixed (byte* pel = el, pm = multi, pd = div)
        {
            for (var x = 0; x < n; x++)
            {
                var rowX = x * width;

                if (pel[rowX + x] == 0)
                {
                    var pivotRow = x + 1;

                    while (pivotRow < n &&
                           pel[(pivotRow * width) + x] == 0)
                    {
                        pivotRow++;
                    }

                    if (pivotRow >= n)
                    {
                        throw new InvalidDataException(
                            "The decoding matrix is singular.");
                    }

                    var rowY = pivotRow * width;

                    // Columns before x are already zero.
                    for (var u = x; u < width; u++)
                    {
                        var temp = pel[rowX + u];
                        pel[rowX + u] = pel[rowY + u];
                        pel[rowY + u] = temp;
                    }
                }

                var pivot = pel[rowX + x];

                if (pivot != 1)
                {
                    pel[rowX + x] = 1;

                    for (var u = x + 1; u < width; u++)
                    {
                        var value = pel[rowX + u];
                        pel[rowX + u] = pd[(value << 8) | pivot];
                    }
                }

                for (var y = 0; y < n; y++)
                {
                    if (y == x)
                    {
                        continue;
                    }

                    var rowY = y * width;
                    var factor = pel[rowY + x];

                    if (factor == 0)
                    {
                        continue;
                    }

                    // pivot == 1, therefore this entry always becomes zero.
                    pel[rowY + x] = 0;

                    for (var u = x + 1; u < width; u++)
                    {
                        pel[rowY + u] ^=
                            pm[(pel[rowX + u] << 8) | factor];
                    }
                }
            }
        }
    }

    private byte[]? rentEF;
    private byte[]? rentEL;
    private byte[][]? rentS;
    private byte[]? rentER;
    private byte[][]? rentEncodeBuffer;
    private byte[]? rentDecodeBuffer;

    private string MatrixToString(byte[] m)
    {
        int row;
        int column;

        var length = m.Length;

        if (length == this.DataSize * this.DataSize)
        {
            row = this.DataSize;
            column = this.DataSize;
        }
        else if (length == this.DataSize * this.DataSize * 2)
        {
            row = this.DataSize;
            column = this.DataSize * 2;
        }
        else if ((length % this.DataSize) == 0)
        {
            row = length / this.DataSize;
            column = this.DataSize;
        }
        else
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        for (var y = 0; y < row; y++)
        {
            var offset = y * column;

            for (var x = 0; x < column; x++)
            {
                sb.AppendFormat("{0,3}", m[offset + x]);
                sb.Append(", ");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private void EnsureBuffers(bool decodeBuffer)
    {
        if (this.rentEF is null)
        {
            this.rentEF =
                ArrayPool<byte>.Shared.Rent(this.DataSize * this.CheckSize);
        }

        if (!decodeBuffer)
        {
            return;
        }

        if (this.rentEL is null)
        {
            this.rentEL =
                ArrayPool<byte>.Shared.Rent(this.DataSize * this.DataSize * 2);
        }

        if (this.rentER is null)
        {
            this.rentER =
                ArrayPool<byte>.Shared.Rent(this.DataSize * this.DataSize);
        }

        if (this.rentS is null)
        {
            this.rentS =
                ArrayPool<byte[]>.Shared.Rent(this.DataSize);
        }
    }

    private void ReturnBuffers()
    {
        if (this.rentEF is not null)
        {
            ArrayPool<byte>.Shared.Return(this.rentEF);
            this.rentEF = null;
        }

        if (this.rentEL is not null)
        {
            ArrayPool<byte>.Shared.Return(this.rentEL);
            this.rentEL = null;
        }

        if (this.rentER is not null)
        {
            ArrayPool<byte>.Shared.Return(this.rentER);
            this.rentER = null;
        }

        if (this.rentS is not null)
        {
            ArrayPool<byte[]>.Shared.Return(this.rentS, clearArray: true);
            this.rentS = null;
        }
    }

    private void EnsureEncodeBuffer(int length)
    {
        var buffers = this.rentEncodeBuffer;
        var pool = ArrayPool<byte>.Shared;

        if (buffers is null)
        {
            buffers = new byte[this.TotalSize][];

            for (var i = 0; i < buffers.Length; i++)
            {
                buffers[i] = pool.Rent(length);
            }

            this.rentEncodeBuffer = buffers;
            return;
        }

        for (var i = 0; i < buffers.Length; i++)
        {
            var buffer = buffers[i];

            if (buffer is null)
            {
                buffers[i] = pool.Rent(length);
            }
            else if (buffer.Length < length)
            {
                pool.Return(buffer);
                buffers[i] = pool.Rent(length);
            }
        }
    }

    private void ReturnEncodeBuffer()
    {
        var buffers = this.rentEncodeBuffer;
        if (buffers is null)
        {
            return;
        }

        var pool = ArrayPool<byte>.Shared;

        for (var i = 0; i < buffers.Length; i++)
        {
            var buffer = buffers[i];

            if (buffer is not null)
            {
                pool.Return(buffer);
                buffers[i] = null!;
            }
        }

        this.rentEncodeBuffer = null;
    }

    private void EnsureDecodeBuffer(int length)
    {
        var buffer = this.rentDecodeBuffer;

        if (buffer is null)
        {
            this.rentDecodeBuffer = ArrayPool<byte>.Shared.Rent(length);
        }
        else if (buffer.Length < length)
        {
            ArrayPool<byte>.Shared.Return(buffer);
            this.rentDecodeBuffer = ArrayPool<byte>.Shared.Rent(length);
        }
    }

    private void ReturnDecodeBuffer()
    {
        if (this.rentDecodeBuffer is null)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(this.rentDecodeBuffer);
        this.rentDecodeBuffer = null;
    }

#pragma warning disable SA1124 // Do not use regions
    #region IDisposable Support
#pragma warning restore SA1124 // Do not use regions

    private bool disposed;

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Frees managed resources.
    /// </summary>
    /// <param name="disposing">true to free managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        if (disposing)
        {
            this.ReturnBuffers();
            this.ReturnDecodeBuffer();
            this.ReturnEncodeBuffer();
        }

        this.disposed = true;
    }

    #endregion
}
