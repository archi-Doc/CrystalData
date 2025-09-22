// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Tinyhand.IO;

namespace CrystalData.Journal;

public class EmptyJournal : IJournal
{
    public static readonly EmptyJournal Default = new();

    Task<CrystalResult> IJournal.Prepare(PrepareParam param)
    {
        return Task.FromResult(CrystalResult.Success);
    }

    int IJournal.MaxRecordLength => 0;

    ulong IJournal.Add(ref TinyhandWriter writer)
    {
        return 0;
    }

    void IJournal.GetWriter(JournalType recordType, out TinyhandWriter writer)
    {
        writer = default(TinyhandWriter);
    }

    Task<CrystalResult> IPersistable.StoreData(StoreMode storeMode, CancellationToken cancellationToken)
        => Task.FromResult(CrystalResult.Success);

    Type IPersistable.DataType => typeof(EmptyJournal);

    Task<bool> IPersistable.TestJournal()
        => Task.FromResult(true);

    Task IJournal.Terminate()
        => Task.CompletedTask;

    ulong IJournal.GetStartingPosition() => 1;

    ulong IJournal.GetCurrentPosition() => 1;

    void IJournal.ResetJournal(ulong position)
    {
    }

    Task<(ulong NextPosition, BytePool.RentMemory Data)> IJournal.ReadJournalAsync(ulong position)
        => Task.FromResult((default(ulong), default(BytePool.RentMemory)));
}
