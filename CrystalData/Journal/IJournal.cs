﻿// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Tinyhand.IO;

namespace CrystalData;

public interface IJournal
{
    Task<CrystalResult> Prepare(PrepareParam param);

    void GetWriter(JournalType recordType, out TinyhandWriter writer);

    ulong Add(ref TinyhandWriter writer);

    Task SaveJournalAsync();

    Task TerminateAsync();

    ulong GetStartingPosition();

    ulong GetCurrentPosition();

    void ResetJournal(ulong position);

    Task<(ulong NextPosition, BytePool.RentMemory Data)> ReadJournalAsync(ulong position);
}
