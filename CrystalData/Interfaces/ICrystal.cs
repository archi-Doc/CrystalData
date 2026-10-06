// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Defines the non-generic lifecycle and storage contract for a registered crystal.
/// </summary>
public interface ICrystal : IStructuralObject, IStructuralRoot, IPersistable
{
    CrystalControl CrystalControl { get; }

    CrystalConfiguration CrystalConfiguration { get; }

    IJournal? Journal { get; }

    IStorage Storage { get; }

    CrystalState State { get; }

    /// <summary>
    /// Gets the cached data, loading it synchronously when necessary.
    /// </summary>
    /// <exception cref="IOException">The data could not be prepared or loaded.</exception>
    /// <remarks>Coordinate application mutations with saves; this property does not grant a mutation lock.</remarks>
    object Data { get; }

    /// <summary>
    /// Reconfigures persistence while retaining any cached data.
    /// </summary>
    /// <param name="configuration">The new persistence configuration.</param>
    /// <remarks>Prepare the crystal again before saving. The next save writes the retained data to the configured destination.</remarks>
    void Configure(CrystalConfiguration configuration);

    /// <summary>
    /// Changes the snapshot file while retaining any cached data.
    /// </summary>
    /// <param name="configuration">The new snapshot file.</param>
    /// <remarks>Prepare the crystal again before saving to the new file.</remarks>
    void ConfigureFile(FileConfiguration configuration);

    /// <summary>
    /// Changes auxiliary storage while retaining any cached root data.
    /// </summary>
    /// <param name="configuration">The new auxiliary-storage configuration.</param>
    /// <remarks>Prepare the crystal again before saving. Existing auxiliary files are not migrated.</remarks>
    void ConfigureStorage(StorageConfiguration configuration);

    /// <summary>
    /// Prepares persistence and loads data unless an instance is already cached.
    /// </summary>
    /// <param name="useQuery">Whether to consult the configured recovery query.</param>
    /// <returns>The preparation or load result.</returns>
    Task<CrystalResult> PrepareAndLoad(bool useQuery);

    Task<CrystalResult> Delete();
}

/// <summary>
/// Provides strongly typed access to a registered crystal.
/// </summary>
/// <typeparam name="TData">The serializable data type.</typeparam>
public interface ICrystal<TData> : ICrystal
    where TData : class, ITinyhandSerializable<TData>, ITinyhandReconstructable<TData>
{
    /// <inheritdoc cref="ICrystal.Data"/>
    public new TData Data { get; }
}
