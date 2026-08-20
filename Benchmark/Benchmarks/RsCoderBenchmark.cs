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
    private const int N = 8;
    private const int M = 4;

    private readonly byte[] source;

    [Params(100)]
    public int Length { get; set; }

    public RsCoderBenchmark()
    {
        this.source = new byte[144];
        this.source.AsSpan().Fill(12);
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
        using (var coder = new RsCoder(N, M))
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
}
