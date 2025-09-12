// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Tinyhand.IO;

namespace CrystalData;

internal interface IReconstructor
{
    bool ReadValueRecord(ref TinyhandReader reader);
}

public static class JournalExtensions
{
    public static async Task<bool> RestoreData<TData>(this IJournal journal, ulong startPosition, TData data, uint plane, ulong pointId = 0)
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
                if (!RestoreFromMemory(startPosition, journalResult.Data.Memory, ref data, plane, pointId))
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

    private static bool RestoreFromMemory<TData>(ulong position, ReadOnlyMemory<byte> memory, ref TData data, uint targetPlane, ulong targetPointId)
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

                    if (targetPointId == 0)
                    {// No point id specified, read all
                        if (!ReadValueRecord(ref reader, ref data))
                        {// Failure
                            result = false;
                        }
                    }
                    else
                    {// Point id specified, read only matching point id
                        reader.Read_Locator();
                        var pointId = reader.ReadUInt64();
                        if (pointId == targetPointId)
                        {// Matching point id
                            if (!ReadValueRecord(ref reader, ref data))
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

    /// <summary>
    /// This function targets CrystalObject or StorageObject.
    /// - CrystalObject JournalRecord: contains only Key or Locator
    /// - StorageObject JournalRecord: may also include AddItem, etc.
    /// Processing AddItem updates the StorageId and causes issues during restore,
    /// so only Key, Locator, and Value JournalRecords are processed.
    /// </summary>
    private static bool ReadValueRecord<TData>(ref TinyhandReader reader, ref TData data)
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
            if (TinyhandSerializer.Deserialize<TData>(ref reader) is { } newData)
            {
                data = newData;
                return true;
            }
            else
            {
                return false;
            }
        }

        return true;
    }
}
