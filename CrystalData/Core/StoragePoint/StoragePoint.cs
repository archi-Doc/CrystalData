// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CrystalData.Internal;
using Tinyhand.IO;

namespace CrystalData;

#pragma warning disable SA1204 // Static elements should appear before instance elements
#pragma warning disable SA1401 // Fields should be private

/// <summary>
/// Represents an independently loaded and persisted node in a structural data tree.<br/>
/// Key <c>0</c> is reserved for the point identifier; derived types must use keys starting at <c>1</c>.
/// </summary>
/// <typeparam name="TData">The stored reference type.</typeparam>
[TinyhandObject(ExplicitKeysOnly = true, ReservedKeyCount = 1)]
public partial class StoragePoint<TData> : ITinyhandSerializable<StoragePoint<TData>>, ITinyhandReconstructable<StoragePoint<TData>>, ITinyhandCloneable<StoragePoint<TData>>, IStructuralObject, IDataLocker<TData>
    where TData : class
{// object:16, ulong:8, StorageObject:8, Structural: 20
    #region FiendAndProperty

    [Key(0)]
    protected ulong pointId; // Lock:StorageControl

    private StorageObject? storageObject; // Lock:StorageControl

    public ulong PointId => this.pointId;

    ref byte IDataLocker<TData>.GetProtectionStateRef() => ref this.GetOrCreateStorageObject().protectionState;

    /// <summary>
    /// Gets the <see langword="uint"/> type identifier used by TinyhandSerializer.
    /// </summary>
    public uint TypeIdentifier => TinyhandTypeIdentifier.GetTypeIdentifier<TData>();

    /// <summary>
    /// Gets a value indicating whether this <see cref="StorageObject"/> is associated with an enabled <see cref="StorageMap"/>.
    /// </summary>
    public bool IsEnabled => this.GetOrCreateStorageObject().IsEnabled;

    /// <summary>
    /// Gets a value indicating whether storage is locked.<br/>
    /// Reading is possible, but writing or unloading is not allowed.
    /// </summary>
    public bool IsLocked => this.storageObject?.IsLocked == true;

    public bool IsDeleted => this.storageObject?.IsDeleted == true;

    /// <summary>
    /// Gets a value indicating whether the in-memory <c>data</c> is pinned.
    /// </summary>
    public bool IsPinned => this.storageObject?.IsPinned == true;

    /// <summary>
    /// Gets a value indicating whether this object is not lockable.
    /// </summary>
    public bool IsNotLockable => this.storageObject?.IsNotLockable == true;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="StoragePoint{TData}"/> class.
    /// </summary>
    public StoragePoint()
    {
    }

    /// <summary>
    /// Sets the data instance for this storage point.<br/>
    /// This function is not recommended, as instance replacement may cause data inconsistencies.
    /// </summary>
    /// <param name="data">The data to set.</param>
    public void Set(TData data)
        => this.GetOrCreateStorageObject().Set(data);

    #region IDataLocker

    /// <summary>
    /// Asynchronously gets the data associated with this storage point.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for the acquisition. If <see cref="TimeSpan.Zero"/>, the method returns immediately.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while waiting to acquire the data.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask{TData}"/> representing the asynchronous operation. The result contains the data if available; otherwise, <c>null</c>.
    /// </returns>
    public ValueTask<TData?> TryGet(TimeSpan timeout, CancellationToken cancellationToken)
        => this.GetOrCreateStorageObject().TryGet<TData>(timeout, cancellationToken);

    public ValueTask<TData?> TryGet()
        => this.GetOrCreateStorageObject().TryGet<TData>(ValueLinkSettings.LockTimeout, default);

    /*/// <summary>
    /// Asynchronously gets the data associated with this storage point, or creates it if it does not exist.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask{TData}"/> representing the asynchronous operation. The result contains the data.
    /// </returns>
    public ValueTask<TData> GetOrCreate()
        => this.GetOrCreateStorageObject().GetOrCreate<TData>();*/

    /// <summary>
    /// Acquires exclusive access to the data and its storage lifecycle.
    /// </summary>
    /// <remarks>Check the returned scope's validity and dispose it before awaiting persistence. Use a consistent parent-to-child lock order.</remarks>
    /// <param name="acquisitionMode">The data acquisition mode specifying get, create, or get-or-create behavior.</param>
    /// <param name="timeout">The maximum time to wait for the lock. If <see cref="TimeSpan.Zero"/>, the method returns immediately.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while waiting to acquire the lock.
    /// </param>
    /// <param name="factory">An optional factory function to create the data instance if it does not exist.</param>
    /// <returns>
    /// A valid data scope when acquisition succeeds; otherwise, a scope describing the failure.
    /// </returns>
    public ValueTask<DataScope<TData>> TryLock(AcquisitionMode acquisitionMode, TimeSpan timeout, CancellationToken cancellationToken = default, Func<IStructuralObject, TData>? factory = default)
        => this.GetOrCreateStorageObject().TryLock<TData>(this, acquisitionMode, timeout, cancellationToken, factory);

    /// <summary>
    /// Acquires a disposable data scope using the default lock timeout.
    /// </summary>
    /// <param name="acquisitionMode">The get, create, or get-or-create behavior.</param>
    /// <param name="factory">An optional factory for new data.</param>
    /// <returns>A valid data scope on success, or a scope describing the failure.</returns>
    /// <remarks>Dispose the scope before awaiting a save of this point or its containing crystal.</remarks>
    public ValueTask<DataScope<TData>> TryLock(AcquisitionMode acquisitionMode = AcquisitionMode.GetOrCreate, Func<IStructuralObject, TData>? factory = default)
        => this.GetOrCreateStorageObject().TryLock<TData>(this, acquisitionMode, ValueLinkSettings.LockTimeout, default, factory);

    ValueTask<DataScope<TData>> IDataLocker<TData>.TryLock(AcquisitionMode acquisitionMode, TimeSpan timeout, CancellationToken cancellationToken, Func<IStructuralObject, TData>? factory)
        => this.GetOrCreateStorageObject().TryLock<TData>(this, acquisitionMode, timeout, cancellationToken, factory);

    /// <summary>
    /// Releases the lock previously acquired by <see cref="TryLock(AcquisitionMode, Func{IStructuralObject, TData}?)"/>.<br/>
    /// To prevent deadlocks, always maintain a consistent lock order and never forget to unlock.
    /// </summary>
    public void Unlock() => this.GetOrCreateStorageObject().Unlock();

    /// <summary>
    /// Pins the data associated with this storage point in memory.<br/>
    /// This prevents eviction but does not grant exclusive mutation access.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask{TData}"/> representing the asynchronous operation.<br/>
    /// The result contains the pinned data.
    /// </returns>
    public ValueTask<TData> PinData()
        => this.GetOrCreateStorageObject().PinData<TData>();

    /// <summary>
    /// Adds this storage point to the save queue.<br/>
    /// The save queue is used to schedule data persistence operations.
    /// </summary>
    /// <param name="delaySeconds">
    /// The number of seconds to delay before saving.<br/>
    /// If 0 is specified, the default delay time is used.
    /// </param>
    public void AddToSaveQueue(int delaySeconds = 0)
        => ((IStructuralRoot)this.GetOrCreateStorageObject()).AddToSaveQueue(delaySeconds);

    #endregion

    public bool DataEquals(StoragePoint<TData> other)
    {
        var data = this.TryGet().AsTask().GetAwaiter().GetResult();
        var otherData = other.TryGet().AsTask().GetAwaiter().GetResult();
        if (data is null)
        {
            return otherData is null;
        }
        else
        {
            return data.Equals(otherData);
        }
    }

    public bool DataEquals(TData? otherData)
    {
        var data = this.TryGet().AsTask().GetAwaiter().GetResult();
        if (data is null)
        {
            return otherData is null;
        }
        else
        {
            return data.Equals(otherData);
        }
    }

    public void DeleteLatestStorageForTest()
        => this.GetOrCreateStorageObject().DeleteLatestStorageForTest();

    #region IStructuralObject

    IStructuralRoot? IStructuralObject.StructuralRoot { get; set; }

    IStructuralObject? IStructuralObject.StructuralParent { get; set; }

    int IStructuralObject.StructuralKey { get; set; }

    /// <summary>
    /// Saves the node and releases unpinned data only after a successful save when requested.
    /// </summary>
    /// <param name="storeMode">The persistence and release mode.</param>
    /// <returns>Whether the node and its children were saved; false can indicate a locked node during try-release.</returns>
    /// <remarks>Store-only and force-release wait for the node lock. Dispose mutation scopes before awaiting this method.</remarks>
    /// <exception cref="IOException">A storage write fails.</exception>
    public Task<bool> StoreData(StoreMode storeMode)
    {
        if (this.storageObject is { } storageObject)
        {
            return storageObject.StoreData(storeMode);
        }
        else
        {
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Deletes the data associated with this storage point.<br/>
    /// This operation removes the data from storage and memory.
    /// </summary>
    /// <param name="forceDeleteAfter">The UTC <see cref="DateTime"/> after which the object will be forcibly deleted if not already deleted.<br/>
    /// <see langword="default"/>: Do not forcibly delete; wait until all operations are finished.<br/>
    /// <see cref="DateTime.UtcNow"/> or earlier: forcibly delete data without waiting.
    /// </param>
    /// <param name="writeJournal">Indicates whether to write the deletion operation to the journal.</param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous delete operation.
    /// </returns>
    public virtual Task DeleteData(DateTime forceDeleteAfter = default, bool writeJournal = true)
        => this.GetOrCreateStorageObject().DeleteData(forceDeleteAfter, writeJournal);

    /*void IStructuralObject.SetupStructure(IStructuralObject? parent, int key)
    {
        ((IStructuralObject)this).StructuralRoot = parent?.StructuralRoot;

        if (this.storageObject is not null)
        {
            if (parent?.StructuralRoot is ICrystal crystal)
            {
                StorageControl.Default.GetOrCreate<TData>(ref this.pointId, ref this.storageObject, crystal.Storage.StorageMap);
            }

            ((IStructuralObject)this.storageObject).SetupStructure(parent, key);
        }
    }*/

    bool IStructuralObject.ProcessJournalRecord(ref TinyhandReader reader)
    {
        if (reader.TryReadJournalRecord(out JournalRecord record))
        {
            if (record == JournalRecord.Value)
            {
                this.pointId = reader.ReadUInt64();
                return true;
            }
        }

        return false;
    }

    #endregion

    Task IDataLocker<TData>.DeletePoint(DateTime forceDeleteAfter, bool writeJournal)
        => this.GetOrCreateStorageObject().DeleteData(forceDeleteAfter, writeJournal);

    #region Tinyhand

    static void ITinyhandSerializable<StoragePoint<TData>>.Serialize(ref TinyhandWriter writer, scoped ref StoragePoint<TData>? v, TinyhandSerializerOptions options)
    {
        if (v is null)
        {
            writer.WriteNil();
        }
        else if (v.storageObject is null)
        {// In-class
            writer.Write(v.pointId);
        }
        else
        {// StorageObject (In-class or Storage disabled)
            if (v.pointId == 0)
            {
                var storageMap = v.GetStorageMap();
                if (storageMap.IsEnabled)
                {// Assign a new PointId and move it to the appropriate StorageMap.
                    storageMap.StorageControl.GetOrCreate<TData>(ref v.pointId, ref v.storageObject, storageMap);
                }
            }

            v.storageObject.SerializeStoragePoint(ref writer, options);
        }
    }

    static unsafe void ITinyhandSerializable<StoragePoint<TData>>.Deserialize(ref TinyhandReader reader, scoped ref StoragePoint<TData>? v, TinyhandSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            v = default;
            return;
        }

        v ??= new();
        if (reader.TryReadUInt64(out var pointId))
        {
            // If the type is interger, it is treated as PointId; otherwise, deserialization is attempted as TData (since TData is not expected to be of interger type, this should generally work without issue).
            v.pointId = pointId;
        }
        else
        {
            StorageMap.Disabled.StorageControl.GetOrCreate<TData>(ref v.pointId, ref v.storageObject, StorageMap.Disabled);
            v.storageObject.SetTypeIdentifier<TData>(); // If the TypeIdentifier is changed, serialization becomes impossible, so update it.

            var data = TinyhandSerializer.Deserialize<TData>(ref reader, options) ?? TinyhandSerializer.Reconstruct<TData>(options);
            v.storageObject.Set(data);
        }
    }

    static unsafe void ITinyhandReconstructable<StoragePoint<TData>>.Reconstruct([NotNull] scoped ref StoragePoint<TData>? v, TinyhandSerializerOptions options)
    {
        v ??= new();
    }

    static unsafe StoragePoint<TData>? ITinyhandCloneable<StoragePoint<TData>>.Clone(scoped ref StoragePoint<TData>? v, TinyhandSerializerOptions options)
    {
        if (v is null)
        {
            return null;
        }

        var obj = new StoragePoint<TData>();
        obj.pointId = v.pointId;
        return obj;
    }

    #endregion

    private StorageObject GetOrCreateStorageObject()
    {
        if (this.storageObject is not null)
        {
            return this.storageObject;
        }

        var storageMap = this.GetStorageMap();
        var previousPointId = this.pointId;
        storageMap.StorageControl.GetOrCreate<TData>(ref this.pointId, ref this.storageObject, storageMap);
        this.storageObject.SetTypeIdentifier<TData>(); // If the TypeIdentifier is changed, serialization becomes impossible, so update it.

        if (this.pointId != previousPointId &&
            ((IStructuralObject)this).TryGetJournalWriter(out var root, out var writer, true) == true)
        {
            writer.Write(JournalRecord.Value);
            writer.Write(this.pointId);
            root.AddJournalAndDispose(ref writer);
        }

        return this.storageObject;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private StorageMap GetStorageMap()
    {
        if (((IStructuralObject)this).StructuralRoot is ICrystal crystal)
        {
            return crystal.Storage.StorageMap;
        }
        else if (((IStructuralObject)this).StructuralRoot is StorageObject storageObject)
        {
            return storageObject.storageMap;
        }
        else
        {
            return StorageMap.Disabled;
        }
    }
}
