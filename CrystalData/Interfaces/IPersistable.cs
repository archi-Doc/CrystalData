// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Defines persistence and journal-integrity operations.
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
    /// <remarks>Check the result before treating data as saved. Child persistence may also throw an I/O exception.</remarks>
    Task<CrystalResult> StoreData(StoreMode storeMode = StoreMode.StoreOnly, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests the integrity of the journal associated with the persisted data.
    /// </summary>
    /// <returns>A <see cref="Task{Boolean}"/> representing the asynchronous operation, with the result indicating whether the journal is valid.</returns>
    Task<bool> TestJournal();
}
