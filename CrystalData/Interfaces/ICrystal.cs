// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public interface ICrystal : IStructualRoot, IPersistable
{
    Crystalizer Crystalizer { get; }

    CrystalConfiguration CrystalConfiguration { get; }

    bool IsConfigured => this.CrystalConfiguration != CrystalConfiguration.Default;

    object Data { get; }

    CrystalState State { get; }

    IStorage Storage { get; }

    IJournal? Journal { get; }

    void Configure(CrystalConfiguration configuration);

    void ConfigureFile(FileConfiguration configuration);

    void ConfigureStorage(StorageConfiguration configuration);

    Task<CrystalResult> PrepareAndLoad(bool useQuery);

    Task<CrystalResult> Delete();
}

public interface ICrystal<TData> : ICrystal
    where TData : class, ITinyhandSerializable<TData>, ITinyhandReconstructable<TData>
{
    public new TData Data { get; }
}

internal interface ICrystalInternal : ICrystal
{
    CrystalConfiguration OriginalCrystalConfiguration { get; }

    Waypoint Waypoint { get; }

    ulong LeadingJournalPosition { get; }

    void SetStorage(IStorage storage);

    Task? TryPeriodicStore(DateTime utc);
}

/*internal interface ICrystalInternal<TData> : ICrystal<TData>, ICrystalInternal
    where TData : class, ITinyhandSerializable<TData>, ITinyhandReconstructable<TData>
{
}*/
