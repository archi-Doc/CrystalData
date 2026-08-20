// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using Xunit;

namespace XunitTest;

public class RsCoderTest
{
    [Theory]
    /*[InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(8, 4)]
    [InlineData(10, 5)]
    [InlineData(16, 8)]*/
    [InlineData(128, 64)]
    public void EncodeDecodeRandomDataAndShards(int n, int m)
    {
        const int Iterations = 1_000;

        var random = new Random(12345);
        var coder = new CrystalData.RsCoder(n, m);

        var shardAvailable = new bool[n + m];
        var indices = new int[n + m];

        for (var i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            var sourceLength = random.Next(1, 128);
            var source = new byte[sourceLength];
            random.NextBytes(source);

            var encoded = coder.Encode(source);

            // Shuffle shard indices.
            for (var i = indices.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            Array.Clear(shardAvailable);

            // Exactly N of N+M shards are available.
            for (var i = 0; i < n; i++)
            {
                shardAvailable[indices[i]] = true;
            }

            var decoded = coder.Decode(encoded, shardAvailable, source.Length);

            Assert.Equal(source, decoded);
        }
    }
}
