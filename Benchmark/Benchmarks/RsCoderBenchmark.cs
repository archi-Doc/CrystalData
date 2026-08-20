// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Numerics;
using Amazon.Auth.AccessControlPolicy;
using BenchmarkDotNet.Attributes;
using CrystalData;
using Netsphere;

namespace Benchmark;

[Config(typeof(BenchmarkConfig))]
public class RsCoderBenchmark
{
    private const int DataCount = 8;
    private const int ParityCount = 4;
    private const int Length = 12 * 1024 * 84;

    private readonly RsCoder2 coder;
    private readonly byte[] source;
    private readonly byte[] destination;
    private readonly bool[] available;

    public RsCoderBenchmark()
    {
        this.source = new byte[Length];
        for (var i = 0; i < this.source.Length; i++)
        {
            this.source[i] = (byte)i;
        }

        this.destination = new byte[Length * (DataCount + ParityCount) / DataCount];

        this.available = [true, false, true, false, false, true, true, true, true, true, true, true,];
        this.coder = new RsCoder2(DataCount, ParityCount);
    }

    [GlobalSetup]
    public void Setup()
    {
    }

    [GlobalCleanup]
    public void Cleanup()
    {
    }

    [Benchmark]
    public byte Test1()
    {
        using (var coder = new RsCoder(DataCount, ParityCount))
        {
            // for (uint i = 0; i < total; i++)
            {
                var mask = ~6U;
                coder.Encode(this.source, this.source.Length);
                coder.InvalidateEncodedBufferForUnitTest(mask);
                coder.Decode(coder.EncodedBuffer!, coder.EncodedBufferLength);
            }
        }

        return this.source[0];
    }

    [Benchmark]
    public int Test2()
    {
        var encoded = this.coder.Encode(this.source);
        var decoded = this.coder.Decode(encoded, this.available, this.source.Length);

        return decoded.Length;
    }

    [Benchmark]
    public int Test3()
    {
        this.coder.Encode(this.source, this.destination);
        this.coder.Decode(this.destination, this.available, this.source.Length, this.source);

        return this.source[0];
    }
}
