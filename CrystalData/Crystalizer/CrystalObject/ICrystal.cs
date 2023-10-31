﻿// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public interface ICrystal : IStructualRoot
{
    Crystalizer Crystalizer { get; }

    CrystalConfiguration CrystalConfiguration { get; }

    bool IsConfigured => this.CrystalConfiguration != CrystalConfiguration.Default;

    Type DataType { get; }

    object Data { get; }

    CrystalState State { get; }

    IStorage Storage { get; }

    IJournal? Journal { get; }

    void Configure(CrystalConfiguration configuration);

    void ConfigureFile(FileConfiguration configuration);

    void ConfigureStorage(StorageConfiguration configuration);

    // IStorageObsolete GetStorage(StorageConfiguration? configuration);

    Task<CrystalResult> PrepareAndLoad(bool useQuery);

    Task<CrystalResult> Save(UnloadMode unloadMode = UnloadMode.NoUnload);

    Task<CrystalResult> Delete();

    void Terminate();
}

public interface ICrystal<TData> : ICrystal
    where TData : class, ITinyhandSerialize<TData>, ITinyhandReconstruct<TData>
{
    public new TData Data { get; }
}

internal interface ICrystalInternal : ICrystal
{
    Task? TryPeriodicSave(DateTime utc);

    Task<bool> TestJournal();

    Waypoint Waypoint { get; }
}

internal interface ICrystalInternal<TData> : ICrystal<TData>, ICrystalInternal
    where TData : class, ITinyhandSerialize<TData>, ITinyhandReconstruct<TData>
{
}
