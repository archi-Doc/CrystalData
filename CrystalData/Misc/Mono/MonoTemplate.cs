// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// An example of <see cref="MonoData{TIdentifier, TDatum}"/>.
/// </summary>
[TinyhandObject]
internal sealed partial class MonoExample
{
    public const string Filename = "MonoExample.tinyhand";

    public MonoExample()
    {
    }

    [KeyAsName]
    public MonoData<int, string> TestData { get; private set; } = new(1_000);
}
