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

    private const int DataCount2 = 32;
    private const int ParityCount2 = 16;

    private readonly RsCoder coder;
    private readonly RsCoder coder2;
    private readonly byte[] source;
    private readonly byte[] destination;
    private readonly bool[] available;
    private readonly bool[] available2;

    public RsCoderBenchmark()
    {
        this.source = new byte[Length];
        for (var i = 0; i < this.source.Length; i++)
        {
            this.source[i] = (byte)i;
        }

        this.destination = new byte[Length * (DataCount + ParityCount) / DataCount];

        this.available = [true, false, true, false, false, true, true, true, true, true, true, true,];
        this.coder = new RsCoder(DataCount, ParityCount);
        this.coder2 = new RsCoder(DataCount2, ParityCount2);

        this.available2 = new bool[DataCount2 + ParityCount2];
        var indices = new int[this.available2.Length];

        for (var i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        // Fisher-Yates shuffle.
        for (var i = indices.Length - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        // Randomly select exactly DataCount2 elements.
        for (var i = 0; i < DataCount2; i++)
        {
            this.available2[indices[i]] = true;
        }
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
    public int EncodeAndDecode_8_4()
    {
        var encoded = this.coder.Encode(this.source);
        var decoded = this.coder.Decode(encoded, this.available, this.source.Length);

        return decoded.Length;
    }

    [Benchmark]
    public int EncodeAndDecode_8_4_b()
    {
        this.coder.Encode(this.source, this.destination);
        this.coder.Decode(this.destination, this.available, this.source.Length, this.source);

        return this.source[0];
    }

    [Benchmark]
    public int EncodeAndDecode_16_32()
    {
        this.coder2.Encode(this.source, this.destination);
        this.coder2.Decode(this.destination, this.available2, this.source.Length, this.source);

        return this.source[0];
    }
}
