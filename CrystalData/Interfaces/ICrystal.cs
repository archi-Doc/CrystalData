// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public interface ICrystal : IStructuralObject, IStructuralRoot, IPersistable
{
    CrystalControl CrystalControl { get; }

    CrystalConfiguration CrystalConfiguration { get; }

    IJournal? Journal { get; }

    IStorage Storage { get; }

    CrystalState State { get; }

    object Data { get; }

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
