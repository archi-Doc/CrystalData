// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using CrystalData.Internal;

namespace CrystalData;

/*
/// <summary>
/// Represents a scope over a locked data instance of type <typeparamref name="TData"/>,<br/>
/// providing controlled access and automatic lock management for the underlying storage object.<br/>
/// Disposing the scope releases the associated lock and invalidates the data reference.
/// </summary>
/// <typeparam name="TData">Type of the data instance managed by this scope. Must be a non-nullable type.</typeparam>
public record struct DataScope<TData> : IDisposable
    where TData : notnull
{
    private readonly StorageObject storageObject;
    private TData? data;

    // public StoragePoint<TData> StoragePoint => this.storagePoint;

    /// <summary>
    /// Gets the scoped data instance while the scope is valid; otherwise <c>null</c> after disposal or if lock failed.
    /// </summary>
    public TData? Data => this.data;

    /// <summary>
    /// Gets a value indicating whether the scope currently references valid data<br/>
    /// (i.e., it has not been disposed and the underlying data was successfully obtained).
    /// </summary>
    [MemberNotNullWhen(true, nameof(data))]
    public bool IsValid => this.data is not null;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataScope{TData}"/> struct.<br/>
    /// Intended for internal use by <see cref="StorageObject"/>; creates a scope over the supplied data<br/>
    /// and assumes an associated lock is held by the creator.
    /// </summary>
    /// <param name="storageObject">The storage object that owns the data and supplies the lock semantics.</param>
    /// <param name="data">The data instance; may be <c>null</c> to represent an invalid scope.</param>
    internal DataScope(StorageObject storageObject, TData? data)
    {
        this.storageObject = storageObject;
        this.data = data;
    }

    /// <summary>
    /// Releases the lock associated with this scope (if still valid).<br/>
    /// Subsequent access to <see cref="Data"/> will return <c>null</c>.
    /// </summary>
    public void Dispose()
    {
        if (this.data is not null)
        {
            this.storageObject.Unlock();
            this.data = default;
        }
    }
}*/
