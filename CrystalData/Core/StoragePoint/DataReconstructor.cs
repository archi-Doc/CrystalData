// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Tinyhand.IO;

namespace CrystalData;

internal interface IReconstructor
{
    bool ReadValueRecord(ref TinyhandReader reader);
}

public static class JournalExtensions
{
    public static Task<bool> ReconstructData<TData>(this IJournal journal, ulong startPosition, object? data, uint plane, ulong pointId = 0)
        => ReconstructData(journal, startPosition, data, TinyhandTypeIdentifier.GetTypeIdentifier<TData>(), plane, pointId);

    public static async Task<bool> ReconstructData(this IJournal journal, ulong startPosition, object? data, uint typeIdentifier, uint plane, ulong pointId = 0)
    {
        var result = true;
        var upperLimit = journal.GetCurrentPosition();

        while (startPosition != 0)
        {
            var journalResult = await journal.ReadJournalAsync(startPosition).ConfigureAwait(false);
            if (journalResult.NextPosition == 0)
            {
                break;
            }

            try
            {
                if (!ReconstructFromMemory(startPosition, journalResult.Data.Memory, ref data, typeIdentifier, plane, pointId))
                {
                    result = false;
                }
            }
            finally
            {
                journalResult.Data.Return();
            }

            if (journalResult.NextPosition >= upperLimit)
            {
                break;
            }

            startPosition = journalResult.NextPosition;
        }

        return result;
    }

    private static bool ReconstructFromMemory(ulong position, ReadOnlyMemory<byte> memory, ref object? data, uint typeIdentifier, uint targetPlane, ulong targetPointId)
    {
        var result = true;
        var reader = new TinyhandReader(memory.Span);
        while (reader.Consumed < memory.Length)
        {
            if (!reader.TryReadJournal(out var length, out var journalType))
            {// Not journal
                return false;
            }

            var fork = reader.Fork();
            try
            {
                if (journalType == JournalType.Record)
                {// Record
                    reader.Read_Locator();
                    var plane = reader.ReadUInt32();
                    if (plane != targetPlane)
                    {// Non-matching plane
                        continue;
                    }

                    if (!reader.TryReadJournalRecord(out var journalRecord) ||
                        journalRecord != JournalRecord.Locator)
                    {// No journal record, or not locator
                        continue;
                    }

                    if (targetPointId == 0)
                    {// No point id specified, read all
                        if (!ReadValueRecord(ref reader, ref data, typeIdentifier))
                        {// Failure
                            result = false;
                        }
                    }
                    else
                    {// Point id specified, read only matching point id
                        var pointId = reader.ReadUInt64();
                        if (pointId == targetPointId)
                        {// Matching point id
                            if (!ReadValueRecord(ref reader, ref data, typeIdentifier))
                            {// Failure
                                result = false;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            finally
            {
                reader = fork;
                if (!reader.TryAdvance(length))
                {
                    reader.TryAdvance(reader.Remaining);
                }
            }
        }

        return result;
    }

    private static bool ReadValueRecord(ref TinyhandReader reader, ref object? data, uint typeIdentifier)
    {
        if (reader.TryReadJournalRecord_PeekIfKeyOrLocator(out var record))
        {// Key or Locator
            if (data is IStructualObject structualObject)
            {
                return structualObject.ProcessJournalRecord(ref reader);
            }
            else
            {
                return false;
            }
        }

        if (record == JournalRecord.Value)
        {
            reader.Read_Value();
            data = TinyhandTypeIdentifier.TryDeserializeReader(typeIdentifier, ref reader);
            return data is not null;
        }

        return true;
    }
}
