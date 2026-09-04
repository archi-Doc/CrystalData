// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Defines a bounded key-value collection that evicts older entries.
/// </summary>
/// <typeparam name="TIdentifier">The key type.</typeparam>
/// <typeparam name="TDatum">The value type.</typeparam>
public interface IMonoData<TIdentifier, TDatum>
{
    int Count { get; }

    void SetCapacity(int capacity);

    void Set(in TIdentifier id, in TDatum value);

    bool TryGet(in TIdentifier id, out TDatum value);

    bool Remove(in TIdentifier id);
}
