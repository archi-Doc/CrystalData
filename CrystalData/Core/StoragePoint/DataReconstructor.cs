// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using Tinyhand.IO;

namespace CrystalData;

/// <summary>
/// Provides helpers for reconstructing structural data from journal records.
/// </summary>
public static class JournalExtensions
{
    public static Task<object?> RestoreData<TData>(this IJournal journal, ulong startPosition, ulong upperLimit, TData data, uint plane, ulong pointId = 0)
        => RestoreData(journal, startPosition, upperLimit, data, TinyhandTypeIdentifier.GetTypeIdentifier<TData>(), plane, pointId);

    public static async Task<object?> RestoreData(this IJournal journal, ulong startPosition, ulong upperLimit, object? originalData, uint typeIdentifier, uint plane, ulong pointId = 0)
    {
        var data = originalData;
        var result = true;
        if (upperLimit <= 0)
        {
            upperLimit = journal.GetCurrentPosition();
        }

        while (startPosition != 0 && startPosition < upperLimit)
        {
            var journalResult = await journal.ReadJournalAsync(startPosition).ConfigureAwait(false);
            try
            {
                if (journalResult.NextPosition <= startPosition ||
                    journalResult.NextPosition - startPosition != (ulong)journalResult.Data.Length ||
                    !RestoreFromMemory(startPosition, upperLimit, journalResult.Data.Memory, ref data, typeIdentifier, plane, pointId))
                {
                    result = false;
                    break;
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

        if (result)
        {
            return data;
        }
        else
        {
            return null;
        }
    }

    private static bool RestoreFromMemory(ulong position, ulong upperLimit, ReadOnlyMemory<byte> memory, ref object? data, uint typeIdentifier, uint targetPlane, ulong targetPointId)
    {
        var result = true;
        var reader = new TinyhandReader(memory.Span);
        while (reader.Consumed < memory.Length)
        {
            if (position + (ulong)reader.Consumed >= upperLimit)
            {// The records after the upper limit are not restored (the memory is read by book).
                break;
            }

            if (!reader.TryReadJournalHeader(out var length, out var journalType))
            {// Not journal
                return false;
            }

            if (length > reader.Remaining || (ulong)reader.Consumed > upperLimit - position ||
                (ulong)length > upperLimit - position - (ulong)reader.Consumed)
            {
                return false;
            }

            var recordReader = reader.CreateSubReader(reader.ReadRaw(length));
            try
            {
                if (journalType == JournalType.Record)
                {// Record
                    recordReader.ReadLocatorRecord();
                    var plane = recordReader.ReadUInt32();
                    if (plane != targetPlane)
                    {// Non-matching plane
                        continue;
                    }

                    if (targetPointId == 0)
                    {// No point id specified, read all
                        if (!ReadValueRecord(ref recordReader, ref data, typeIdentifier))
                        {// Failure
                            result = false;
                        }
                    }
                    else
                    {// Point id specified, read only matching point id
                        if (!recordReader.TryPeekJournalRecord(out var record))
                        {
                            return false;
                        }

                        if (record != JournalRecordType.Locator)
                        {// Map-level records do not address a storage point.
                            continue;
                        }

                        recordReader.ReadLocatorRecord();
                        var pointId = recordReader.ReadUInt64();
                        if (pointId == targetPointId)
                        {// Matching point id
                            if (!ReadValueRecord(ref recordReader, ref data, typeIdentifier))
                            {// Failure
                                result = false;
                            }
                        }
                    }
                }
            }
            catch
            {// A malformed matching record must not be reported as successful recovery.
                result = false;
            }
        }

        return result;
    }

    /// <summary>
    /// Restores value and structural changes while ignoring storage-history metadata.
    /// </summary>
    private static bool ReadValueRecord(ref TinyhandReader reader, ref object? data, uint typeIdentifier)
    {
        reader.TryPeekJournalRecord(out var record);

        if (record == JournalRecordType.Value)
        {
            reader.Advance(1);
            data = TinyhandTypeIdentifier.TryDeserialize(typeIdentifier, ref reader);
            return data is not null;

            /* if (TinyhandSerializer.Deserialize<TData>(ref reader) is { } newData)
             {
                 data = newData;
                 return true;
             }
             else
             {
                 return false;
             }*/
        }
        else if (record == JournalRecordType.AddCustom)
        {
            return true;
        }

        // Other (Key or Locator)
        if (data is IStructuralObject structuralObject)
        {
            return structuralObject.ProcessJournalRecord(ref reader);
        }
        else
        {
            return false;
        }
    }
}
