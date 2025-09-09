// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Defines methods and properties for objects that can be persisted and restored.
/// </summary>
public interface IPersistable
{
    /// <summary>
    /// Gets the type of data that is persisted.
    /// </summary>
    Type DataType { get; }

    /// <summary>
    /// Stores the current object asynchronously.
    /// </summary>
    /// <param name="storeMode">The mode in which to store the data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{CrystalResult}"/> representing the asynchronous operation, with the result indicating the outcome.</returns>
    Task<CrystalResult> Store(StoreMode storeMode = StoreMode.StoreOnly, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests the integrity of the journal associated with the persisted data.
    /// </summary>
    /// <returns>A <see cref="Task{Boolean}"/> representing the asynchronous operation, with the result indicating whether the journal is valid.</returns>
    Task<bool> TestJournal();
}
