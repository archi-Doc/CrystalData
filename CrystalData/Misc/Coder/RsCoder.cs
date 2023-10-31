// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Buffers;

#pragma warning disable SA1519 // Braces should not be omitted from multi-line child statement

namespace CrystalData;

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
    public RsCoder(int dataSize = DefaultDataSize, int checkSize = DefaultCheckSize, int fieldGenPoly = GaloisField.FieldGenPoly)
    {
        this.DataSize = dataSize;
        if (dataSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(dataSize));
        }

        this.CheckSize = checkSize;
        if (checkSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(dataSize));
        }

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
        var nm = this.TotalSize;
        var n = this.DataSize;
        var m = this.CheckSize;
        var multi = this.GaloisField.Multi;

        if ((length % n) != 0)
        {
            throw new InvalidDataException("Length of source data must be a multiple of RsCoder.DataSize.");
        }

        this.EncodedBufferLength = length / n;
        this.EnsureEncodeBuffer(this.EncodedBufferLength);
        var destination = this.rentEncodeBuffer!;
        var destinationLength = this.EncodedBufferLength;
        var ef = this.rentEF!;

        /*encode core (original)
        Span<byte> b = source;
        for (var x = 0; x < destinationLength; x++)
        {
            for (var y = 0; y < n; y++)
            {// data
                destination[y][x] = b[y];
            }

            for (var y = 0; y < m; y++)
            {
                var d = 0;
                for (var z = 0; z < n; z++)
                {
                    d ^= multi[b[z], ef[(y * n) + z]];
                }

                destination[n + y][x] = (byte)d;
            }

            b = b.Slice(n);
        }*/

        // encode core (n = 4, 8, other)
        if (n == 4)
        {
            fixed (byte* ps = source, pef = ef, pm = multi)
            fixed (byte* pd0 = destination[0], pd1 = destination[1], pd2 = destination[2], pd3 = destination[3])
            {
                var ps2 = ps;
                for (var x = 0; x < destinationLength; x++)
                {
                    pd0[x] = ps2[0];
                    pd1[x] = ps2[1];
                    pd2[x] = ps2[2];
                    pd3[x] = ps2[3];

                    for (var y = 0; y < m; y++)
                    {
                        var d = 0;
                        var yn = y * n;
                        d ^= pm[(ps2[0] * GaloisField.Max) + pef[yn + 0]];
                        d ^= pm[(ps2[1] * GaloisField.Max) + pef[yn + 1]];
                        d ^= pm[(ps2[2] * GaloisField.Max) + pef[yn + 2]];
                        d ^= pm[(ps2[3] * GaloisField.Max) + pef[yn + 3]];

                        destination[n + y][x] = (byte)d;
                    }

                    ps2 += n;
                }
            }
        }
        else if (n == 8)
        {
            fixed (byte* ps = source, pef = ef, pm = multi)
            fixed (byte* pd0 = destination[0], pd1 = destination[1], pd2 = destination[2], pd3 = destination[3],
                pd4 = destination[4], pd5 = destination[5], pd6 = destination[6], pd7 = destination[7])
            {// 0..n: $"pd{i} = destination[{i}], "
                var ps2 = ps;
                for (var x = 0; x < destinationLength; x++)
                {
                    pd0[x] = ps2[0];
                    pd1[x] = ps2[1];
                    pd2[x] = ps2[2];
                    pd3[x] = ps2[3];
                    pd4[x] = ps2[4];
                    pd5[x] = ps2[5];
                    pd6[x] = ps2[6];
                    pd7[x] = ps2[7];

                    for (var y = 0; y < m; y++)
                    {
                        var d = 0;
                        var yn = y * n;
                        d ^= pm[(ps2[0] * GaloisField.Max) + pef[yn + 0]];
                        d ^= pm[(ps2[1] * GaloisField.Max) + pef[yn + 1]];
                        d ^= pm[(ps2[2] * GaloisField.Max) + pef[yn + 2]];
                        d ^= pm[(ps2[3] * GaloisField.Max) + pef[yn + 3]];
                        d ^= pm[(ps2[4] * GaloisField.Max) + pef[yn + 4]];
                        d ^= pm[(ps2[5] * GaloisField.Max) + pef[yn + 5]];
                        d ^= pm[(ps2[6] * GaloisField.Max) + pef[yn + 6]];
                        d ^= pm[(ps2[7] * GaloisField.Max) + pef[yn + 7]];

                        destination[n + y][x] = (byte)d;
                    }

                    ps2 += n;
                }
            }
        }
        else
        {
            fixed (byte* ps = source, pef = ef, pm = multi)
            {// 0..n: $"pd{i} = destination[{i}], "
                var ps2 = ps;
                for (var x = 0; x < destinationLength; x++)
                {
                    for (var y = 0; y < n; y++)
                    {// 0..n: $"pd{i}[x] = ps2[{i}];"
                        destination[y][x] = ps2[y];
                    }

                    for (var y = 0; y < m; y++)
                    {
                        var d = 0;
                        var yn = y * n;
                        for (var z = 0; z < n; z++)
                        {// 0..n: $"d ^= pm[(ps2[{i}] * GaloisField.Max) + pef[yn + {i}]];"
                            d ^= pm[(ps2[z] * GaloisField.Max) + pef[yn + z]];
                        }

                        destination[n + y][x] = (byte)d;
                    }

                    ps2 += n;
                }
            }
        }
    }

    public unsafe void Decode(byte[]?[] source, int length)
    {
        var nm = this.TotalSize;
        var n = this.DataSize;
        var m = this.CheckSize;
        var multi = this.GaloisField.Multi;

        for (var x = 0; x < nm; x++)
        {
            if (source[x] != null && source[x]!.Length < length)
            {
                throw new InvalidDataException("Length of source byte arrays must be greater than 'length'.");
            }
        }

        this.DecodedBufferLength = length * n;
        this.EnsureDecodeBuffer(this.DecodedBufferLength);
        var destination = this.rentDecodeBuffer!;

        // Rent buffers
        this.EnsureBuffers(true);
        var ef = this.rentEF!;
        var el = this.rentEL!;
        el.AsSpan().Fill(0);

        var u = 0; // data
        var v = 0; // check
        var z = 0;
        var s = this.rentS!;
        for (var x = 0; x < n; x++)
        {
            if (x == u && source[u] != null)
            {// Data
                z = u;
                u++;
            }
            else
            {// Search valid check
                while (source[u] == null && u < n)
                {
                    u++;
                }

                while (source[n + v] == null)
                {
                    v++;
                    if (v >= m)
                    {
                        throw new InvalidDataException("Number of valid byte arrays must be greater than or equal to RsCoder.DataSize.");
                    }
                }

                z = n + v;
                v++;
            }

            if (z < n)
            {// data
                for (var y = 0; y < n; y++)
                {
                    if (y == z)
                    {
                        el[y + (x * n * 2)] = 1;
                    }
                }
            }
            else
            {// check
                for (var y = 0; y < n; y++)
                {
                    el[y + (x * n * 2)] = ef[y + ((z - n) * n)];
                }
            }

            for (var y = 0; y < n; y++)
            {
                if (y == x)
                {
                    el[y + (x * n * 2) + n] = 1;
                }
            }

            s[x] = source[z]!;
        }

        this.GenerateEL(s);

        // copy reverse
        var er = this.rentER!;
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                er[x + (n * y)] = el[n + x + (y * n * 2)];
            }
        }

        // decode core (original)
        /*var i = 0;
        for (var x = 0; x < sourceLength; x++)
        {
            for (var y = 0; y < n; y++)
            {
                u = 0;
                for (z = 0; z < n; z++)
                {
                    u ^= multi[er[z + (y * n)], s[z][x]];
                }

                destination[i++] = (byte)u; // fixed
            }
        }*/

        // decode core (n = 4, 8, other)
        if (n == 4)
        {
            fixed (byte* um = multi, uer = er, ud = destination)
            fixed (byte* ps0 = s[0], ps1 = s[1], ps2 = s[2], ps3 = s[3])
            {
                var ud2 = ud;
                for (var x = 0; x < length; x++)
                {
                    for (var y = 0; y < n; y++)
                    {
                        var yn = y * n;

                        u = um[(uer[0 + yn] * GaloisField.Max) + ps0[x]];
                        u ^= um[(uer[1 + yn] * GaloisField.Max) + ps1[x]];
                        u ^= um[(uer[2 + yn] * GaloisField.Max) + ps2[x]];
                        u ^= um[(uer[3 + yn] * GaloisField.Max) + ps3[x]];

                        *ud2++ = (byte)u; // fixed
                    }
                }
            }
        }
        else if (n == 8)
        {
            fixed (byte* um = multi, uer = er, ud = destination)
            fixed (byte* ps0 = s[0], ps1 = s[1], ps2 = s[2], ps3 = s[3],
ps4 = s[4], ps5 = s[5], ps6 = s[6], ps7 = s[7])
            {
                var ud2 = ud;
                for (var x = 0; x < length; x++)
                {
                    for (var y = 0; y < n; y++)
                    {
                        var yn = y * n;

                        u = um[(uer[0 + yn] * GaloisField.Max) + ps0[x]];
                        u ^= um[(uer[1 + yn] * GaloisField.Max) + ps1[x]];
                        u ^= um[(uer[2 + yn] * GaloisField.Max) + ps2[x]];
                        u ^= um[(uer[3 + yn] * GaloisField.Max) + ps3[x]];
                        u ^= um[(uer[4 + yn] * GaloisField.Max) + ps4[x]];
                        u ^= um[(uer[5 + yn] * GaloisField.Max) + ps5[x]];
                        u ^= um[(uer[6 + yn] * GaloisField.Max) + ps6[x]];
                        u ^= um[(uer[7 + yn] * GaloisField.Max) + ps7[x]];

                        *ud2++ = (byte)u; // fixed
                    }
                }
            }
        }
        else
        {
            fixed (byte* um = multi, uer = er, ud = destination)
            {// $"ps{i} = s[{i}], "
                var ud2 = ud;
                for (var x = 0; x < length; x++)
                {
                    for (var y = 0; y < n; y++)
                    {
                        var yn = y * n;
                        u = 0;
                        for (z = 0; z < n; z++)
                        {// $"u ^= um[(uer[{i} + yn] * GaloisField.Max) + ps{i}[x]];"
                            u ^= um[(uer[z + yn] * GaloisField.Max) + s[z][x]];
                        }

                        *ud2++ = (byte)u; // fixed
                    }
                }
            }
        }
    }

    public override string ToString() => $"RsCoder Data: {this.DataSize}, Check: {this.CheckSize}";

    public void InvalidateEncodedBufferForUnitTest(System.Random random, int number)
    {
        if (this.rentEncodeBuffer == null)
        {
            return;
        }
        else if (this.rentEncodeBuffer.Length < number)
        {
            throw new InvalidOperationException();
        }

        while (true)
        {
            var invalidNumber = this.rentEncodeBuffer.Count(a => a == null);
            if (invalidNumber >= number)
            {
                return;
            }

            int i;
            do
            {
                i = random.Next(this.rentEncodeBuffer.Length);
            }
            while (this.rentEncodeBuffer[i] == null);

            ArrayPool<byte>.Shared.Return(this.rentEncodeBuffer[i]);
            this.rentEncodeBuffer[i] = null!; // Invalidate
        }
    }

    public void InvalidateEncodedBufferForUnitTest(uint bufferbits)
    {
        if (this.rentEncodeBuffer == null)
        {
            return;
        }

        for (var i = 0; i < this.rentEncodeBuffer.Length; i++)
        {
            if ((bufferbits & (1 << i)) == 0)
            {
                ArrayPool<byte>.Shared.Return(this.rentEncodeBuffer[i]);
                this.rentEncodeBuffer[i] = null!; // Invalidate
            }
        }
    }

    public void TestReverseMatrix(uint sourceBits)
    {
        var n = this.DataSize;
        var m = this.CheckSize;

        this.EnsureBuffers(true);
        var ef = this.rentEF!;
        var el = this.rentEL!;
        el.AsSpan().Fill(0);

        var u = 0; // data
        var v = 0; // check
        var z = 0;
        for (var x = 0; x < n; x++)
        {
            if (x == u && ((sourceBits & (1 << u)) != 0))
            {// Data
                z = u;
                u++;
            }
            else
            {// Search valid check
                while (((sourceBits & (1 << u)) == 0) && u < n)
                {
                    u++;
                }

                while ((sourceBits & (1 << (n + v))) == 0)
                {
                    v++;
                    if (v >= m)
                    {
                        throw new InvalidDataException("The number of valid byte arrays must be greater than RsCoder.DataSize.");
                    }
                }

                z = n + v;
                v++;
            }

            if (z < n)
            {// data
                el[z + (x * n * 2)] = 1;
            }
            else
            {// check
                for (var y = 0; y < n; y++)
                {
                    el[y + (x * n * 2)] = ef[y + ((z - n) * n)];
                }
            }

            el[x + (x * n * 2) + n] = 1;
        }

        this.GenerateEL(null);

        // check
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                if (x == y)
                {
                    if (el[x + (y * n * 2)] != 1)
                    {
                        throw new Exception();
                    }
                }
                else
                {
                    if (el[x + (y * n * 2)] != 0)
                    {
                        throw new Exception();
                    }
                }
            }
        }
    }

    private void GenerateEF()
    {
        var ef = this.rentEF!;
        for (var y = 0; y < this.CheckSize; y++)
        {
            for (var x = 0; x < this.DataSize; x++)
            {
                ef[x + (this.DataSize * y)] = this.GaloisField.GFI[x * y]; // 1st
            }
        }

        /*for (var x = 0; x < this.DataSize; x++)
        {
            ef[x] = 1; // 2nd
            // this.F[x] = 1;
        }

        for (var y = 1; y < this.CheckSize; y++)
        {
            for (var x = 0; x < this.DataSize; x++)
            {
                ef[x + (this.DataSize * y)] = this.GaloisField.Multi[(ef[x + (this.DataSize * (y - 1))] * GaloisField.Max) + this.GaloisField.GFI[x]]; // 2nd
                // this.F[x + (this.DataSize * y)] = this.GaloisField.Multi[(this.F[x + (this.DataSize * (y - 1))] * GaloisField.Max) + x + 1]; // Obsolete
            }
        }*/

        /*var temp = new byte[this.DataSize];
        for (var x = 0; x < this.DataSize; x++)
        {
            temp[x] = 1;
        }

        for (var y = 0; y < this.CheckSize; y++)
        {
            for (var x = 0; x < this.DataSize; x++)
            {
                this.F[x + (this.DataSize * y)] = this.GaloisField.GFI[temp[x]];
                temp[x] = this.GaloisField.Multi[((x + 1) * GaloisField.Max) + temp[x]];
            }
        }*/

        // Random...
        /*for (var y = 0; y < ef.Length; y++)
        {
            ef[y] = this.GaloisField.GFI[Random.Shared.Next() & GaloisField.Mask];
        }*/
    }

    private void GenerateEL(byte[][]? s)
    {
        var n = this.DataSize;
        var multi = this.GaloisField.Multi;
        var div = this.GaloisField.Div;
        var el = this.rentEL!;

        for (var x = 0; x < n; x++)
        {
            if (el[x + (x * n * 2)] != 0)
            {
                goto Normalize;
            }

            // Pivoting (Row)
            for (var y = x + 1; y < n; y++)
            {
                if (el[x + (y * n * 2)] != 0)
                {
                    for (var u = 0; u < (n * 2); u++)
                    {
                        var temp = el[u + (y * n * 2)];
                        el[u + (y * n * 2)] = el[u + (x * n * 2)];
                        el[u + (x * n * 2)] = temp;
                    }

                    goto Normalize;
                }
            }

            // Pivoting (Column)
            for (var y = x + 1; y < n; y++)
            {
                if (el[y + (x * n * 2)] != 0)
                {
                    for (var u = 0; u < n; u++)
                    {
                        var temp = el[y + (u * n * 2)];
                        el[y + (u * n * 2)] = el[x + (u * n * 2)];
                        el[x + (u * n * 2)] = temp;
                    }

                    if (s != null)
                    {
                        var temp = s[y];
                        s[y] = s[x];
                        s[x] = temp;
                    }

                    goto Normalize;
                }
            }

            // el[x + (x * n * 2)] is 0...
            throw new InvalidDataException("Sorry for this.");

Normalize:
            var e = el[x + (x * n * 2)];
            if (e != 1)
            {
                for (var y = 0; y < (n * 2); y++)
                {
                    el[y + (x * n * 2)] = div[(el[y + (x * n * 2)] * GaloisField.Max) + e];
                }
            }

            for (var y = 0; y < n; y++)
            {
                if (x != y)
                {
                    e = el[x + (y * n * 2)];
                    if (e != 0)
                    {
                        e = div[(e * GaloisField.Max) + 1];
                        for (var u = 0; u < (n * 2); u++)
                        {
                            el[u + (y * n * 2)] ^= multi[(el[u + (x * n * 2)] * GaloisField.Max) + e];
                        }
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
        int row, column;
        var length = m.Length;
        if (length == (this.DataSize * this.DataSize))
        {
            row = this.DataSize;
            column = this.DataSize;
        }
        else if (length == (this.DataSize * this.DataSize * 2))
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

        var sb = new System.Text.StringBuilder();
        for (var y = 0; y < row; y++)
        {
            for (var x = 0; x < column; x++)
            {
                sb.Append(string.Format("{0, 3}", m[x + (y * column)]));
                sb.Append(", ");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private void EnsureBuffers(bool decodeBuffer)
    {
        if (this.rentEF == null)
        {
            this.rentEF = ArrayPool<byte>.Shared.Rent(this.DataSize * this.CheckSize);
            Array.Fill<byte>(this.rentEF, 0);
        }

        if (decodeBuffer)
        {
            if (this.rentEL == null)
            {
                this.rentEL = ArrayPool<byte>.Shared.Rent(this.DataSize * this.DataSize * 2);
            }

            if (this.rentER == null)
            {
                this.rentER = ArrayPool<byte>.Shared.Rent(this.DataSize * this.DataSize);
            }

            if (this.rentS == null)
            {
                this.rentS = ArrayPool<byte[]>.Shared.Rent(this.DataSize);
            }
        }
    }

    private void ReturnBuffers()
    {
        if (this.rentEF != null)
        {
            ArrayPool<byte>.Shared.Return(this.rentEF);
            this.rentEF = null;
        }

        if (this.rentEL != null)
        {
            ArrayPool<byte>.Shared.Return(this.rentEL);
            this.rentEL = null;
        }

        if (this.rentER != null)
        {
            ArrayPool<byte>.Shared.Return(this.rentER);
            this.rentER = null;
        }

        if (this.rentS != null)
        {
            ArrayPool<byte[]>.Shared.Return(this.rentS);
            this.rentS = null;
        }
    }

    private void EnsureEncodeBuffer(int length)
    {
        if (this.rentEncodeBuffer == null)
        {// Rent a buffer.
            this.rentEncodeBuffer = new byte[this.TotalSize][];
            for (var n = 0; n < this.TotalSize; n++)
            {
                this.rentEncodeBuffer[n] = ArrayPool<byte>.Shared.Rent(length);
            }
        }
        else
        {
            for (var n = 0; n < this.TotalSize; n++)
            {
                if (this.rentEncodeBuffer[n] == null)
                {// Rent
                    this.rentEncodeBuffer[n] = ArrayPool<byte>.Shared.Rent(length);
                }
                else if (this.rentEncodeBuffer[n].Length < length)
                {// Insufficient buffer, return and rent.
                    ArrayPool<byte>.Shared.Return(this.rentEncodeBuffer[n]);
                    this.rentEncodeBuffer[n] = ArrayPool<byte>.Shared.Rent(length);
                }
            }
        }
    }

    private void ReturnEncodeBuffer()
    {
        if (this.rentEncodeBuffer != null)
        {
            for (var n = 0; n < this.rentEncodeBuffer.Length; n++)
            {
                if (this.rentEncodeBuffer[n] != null)
                {
                    ArrayPool<byte>.Shared.Return(this.rentEncodeBuffer[n]!);
                    this.rentEncodeBuffer[n] = null!;
                }
            }

            this.rentEncodeBuffer = null!;
        }
    }

    private void EnsureDecodeBuffer(int length)
    {
        if (this.rentDecodeBuffer == null)
        {// Rent a buffer.
            this.rentDecodeBuffer = ArrayPool<byte>.Shared.Rent(length);
        }
        else if (this.rentDecodeBuffer.Length < length)
        {// Insufficient buffer, return and rent.
            this.ReturnDecodeBuffer();
            this.rentDecodeBuffer = ArrayPool<byte>.Shared.Rent(length);
        }
    }

    private void ReturnDecodeBuffer()
    {
        if (this.rentDecodeBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(this.rentDecodeBuffer);
            this.rentDecodeBuffer = null;
        }
    }

#pragma warning disable SA1124 // Do not use regions
    #region IDisposable Support
#pragma warning restore SA1124 // Do not use regions

    private bool disposed = false; // To detect redundant calls.

    /// <summary>
    /// Finalizes an instance of the <see cref="RsCoder"/> class.
    /// </summary>
    ~RsCoder()
    {
        this.Dispose(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// free managed/native resources.
    /// </summary>
    /// <param name="disposing">true: free managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                // free managed resources.
                this.ReturnBuffers();
                this.ReturnDecodeBuffer();
                this.ReturnEncodeBuffer();
            }

            // free native resources here if there are any.
            this.disposed = true;
        }
    }
    #endregion
}
