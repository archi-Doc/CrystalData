// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Monolithic data store.
/// </summary>
[TinyhandObject]
internal sealed partial class MonoTemplate
{
    public MonoTemplate()
    {
    }

    [KeyAsName]
    public MonoData<int, string> TestData { get; private set; } = new(1_000);
}
