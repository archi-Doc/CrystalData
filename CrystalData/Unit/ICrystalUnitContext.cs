// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public interface ICrystalUnitContext
{
    void AddCrystal<TData>(CrystalConfiguration configuration)
        where TData : class, ITinyhandSerialize<TData>, ITinyhandReconstruct<TData>;

    /*bool TryAddCrystal<TData>(CrystalConfiguration configuration)
        where TData : class, ITinyhandSerialize<TData>, ITinyhandReconstruct<TData>;*/

    void SetJournal(JournalConfiguration configuration);

    bool TrySetJournal(JournalConfiguration configuration);
}
