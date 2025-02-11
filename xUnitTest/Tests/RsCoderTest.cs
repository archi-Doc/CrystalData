// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Numerics;
using CrystalData;
using Xunit;

#pragma warning disable SA1202 // Elements should be ordered by access

namespace xUnitTest;

public class RsCoderTest
{
    [Fact]
    public void ComprehensiveTest()
    {
        // (int Data, int Check)[] nm = new[] { (4, 2), (4, 4), (4, 8), (8, 4), (8, 8), (8, 10), (16, 8), (16, 16), (5, 3), (5, 5), (13, 3), (13, 7), }; // Not supported
        (int Data, int Check)[] nm = [(4, 2), (4, 4), (8, 2), (8, 4), (16, 2), (16, 4),];

        foreach (var x in nm)
        {
            this.ComprehensiveTestNM(x.Data, x.Check);
            this.ComprehensiveTestNM_Data(x.Data, x.Check);
        }
    }

    private void ComprehensiveTestNM_Data(int n, int m)
    {
        var source = new byte[n];
        for (var i = 0; i < n; i++)
        {
            source[i] = (byte)i;
        }

        using (var coder = new RsCoder(n, m))
        {
            var total = 1 << coder.TotalSize;
            for (uint i = 0; i < total; i++)
            {
                if (BitOperations.PopCount(i) < n)
                {// N blocks of valid data is required.
                    continue;
                }

                coder.Encode(source, source.Length);
                coder.InvalidateEncodedBufferForUnitTest(i);
                coder.Decode(coder.EncodedBuffer!, coder.EncodedBufferLength);
                TestHelper.ByteArrayEquals(source, coder.DecodedBuffer, source.Length).IsTrue();
            }
        }
    }

    private void ComprehensiveTestNM(int n, int m)
    {
        using (var coder = new RsCoder(n, m))
        {
            var total = 1 << coder.TotalSize;
            for (uint i = 0; i < total; i++)
            {
                if (BitOperations.PopCount(i) < n)
                {// N blocks of valid data is required.
                    continue;
                }

                coder.TestReverseMatrix(i);
            }
        }
    }

    [Fact]
    public void RandomTest()
    {
        (int Data, int Check)[] nm = [(4, 2), (4, 4), (8, 2), (8, 4), (16, 2), (16, 4),];
        var sizes = new[] { 0, 4, 16, 256, 1000, 10_000 };

        var random = new Random(42);
        var sources = new byte[sizes.Length][];
        for (var n = 0; n < sizes.Length; n++)
        {
            sources[n] = new byte[sizes[n]];
            random.NextBytes(sources[n]);
        }

        foreach (var x in nm)
        {
            this.RandomTestNM(x.Data, x.Check, sources, random);
        }
    }

    private void RandomTestNM(int n, int m, byte[][] sources, Random random)
    {
        // using (var coder = new RsCoder)

        using (var coder = new RsCoder(n, m))
        {
            foreach (var x in sources)
            {
                this.RandomTestSource(coder, x, random);
            }
        }
    }

    private void RandomTestSource(RsCoder coder, byte[] source, Random random)
    {
        var length = source.Length;
        length = (length / coder.DataSize) * coder.DataSize; // length must be a multiple of coder.DataSize

        // Simple encode and decode.
        coder.Encode(source, length);
        coder.Decode(coder.EncodedBuffer!, coder.EncodedBufferLength);
        TestHelper.ByteArrayEquals(source, coder.DecodedBuffer, length).IsTrue();

        for (var i = 1; i <= coder.CheckSize; i++)
        {
            for (var j = 0; j < 10; j++)
            {
                coder.Encode(source, length);
                coder.InvalidateEncodedBufferForUnitTest(random, i);
                coder.Decode(coder.EncodedBuffer!, coder.EncodedBufferLength);
                TestHelper.ByteArrayEquals(source, coder.DecodedBuffer, length).IsTrue();
            }
        }
    }
}
