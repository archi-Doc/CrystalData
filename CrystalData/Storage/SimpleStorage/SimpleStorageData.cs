// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Runtime.CompilerServices;
using Tinyhand.IO;

namespace CrystalData.Storage;

/// <summary>
/// Persists the file index and usage metadata for the built-in auxiliary storage.
/// </summary>
[TinyhandObject(Structural = true)]
public partial class SimpleStorageData : ITinyhandSerializable<SimpleStorageData>, ITinyhandCustomJournal
{
    public SimpleStorageData()
    {
    }

    #region PropertyAndField

    /// <summary>
    /// Gets the sum of all tracked file sizes in bytes.
    /// </summary>
    public long StorageUsage
    {
        get
        {
            using (this.lockObject.EnterScope())
            {
                return this.storageUsage;
            }
        }
    }

    /// <summary>
    /// Gets the number of tracked files.
    /// </summary>
    public int Count
    {
        get
        {
            using (this.lockObject.EnterScope())
            {
                return this.fileToSize.Count;
            }
        }
    }

    private Lock lockObject = new();
    private long storageUsage; // syncObject
    private Dictionary<uint, int> fileToSize = new(); // syncObject

    #endregion

    static void ITinyhandSerializable<SimpleStorageData>.Serialize(ref TinyhandWriter writer, scoped ref SimpleStorageData? value, TinyhandSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        using (value.lockObject.EnterScope())
        {
            // writer.WriteArrayHeader(2);

            // 1st item
            writer.Write(value.storageUsage);

            // 2nd item
            writer.WriteMapHeader(value.fileToSize.Count);
            foreach (var x in value.fileToSize)
            {
                writer.Write(x.Key);
                writer.Write(x.Value);
            }
        }
    }

    static void ITinyhandSerializable<SimpleStorageData>.Deserialize(ref TinyhandReader reader, scoped ref SimpleStorageData? value, TinyhandSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            value = null;
            return;
        }

        value ??= new();
        using (value.lockObject.EnterScope())
        {
            /*if (reader.ReadArrayHeader() != 2)
            {
                return;
            }*/

            // 1st item
            var storageUsage = reader.ReadInt64();

            // 2nd item
            var count = reader.ReadMapHeaderOrEmptyArray();
            var fileToSize = new Dictionary<uint, int>(count);
            long actualUsage = 0;
            for (var i = 0; i < count; i++)
            {
                var file = reader.ReadUInt32();
                var size = reader.ReadInt32();
                if (file == 0 || size < 0 || !fileToSize.TryAdd(file, size))
                {
                    throw new TinyhandException("Invalid storage file index.");
                }

                actualUsage += size;
            }

            if (storageUsage != actualUsage)
            {
                throw new TinyhandException("Storage usage does not match the file index.");
            }

            value.fileToSize = fileToSize;
            value.storageUsage = actualUsage;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(uint file)
    {
        using (this.lockObject.EnterScope())
        {
            if (((IStructuralObject)this).TryGetJournalWriter(out var root, out var writer, false))
            {
                writer.Write(JournalRecordType.DeleteItem);
                writer.Write(file);
                root.AddJournalAndDispose(ref writer);
            }

            return this.TryRemoveFile(file);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(uint file, out int size)
    {
        using (this.lockObject.EnterScope())
        {
            return this.fileToSize.TryGetValue(file, out size);
        }
    }

    public uint[] GetFileArray()
    {
        using (this.lockObject.EnterScope())
        {
            return this.fileToSize.Keys.ToArray();
        }
    }

    /*[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint NewFile(int size)
    {
        using (this.lockObject.EnterScope())
        {
            return this.NewFileInternal(size);
        }
    }*/

    /// <summary>
    /// Inserts or updates a file entry and its accounted size atomically.
    /// </summary>
    /// <param name="file">The existing identifier, or a value replaced with a new identifier when absent.</param>
    /// <param name="dataSize">The non-negative size in bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">The size is negative.</exception>
    public void Put(ref uint file, int dataSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dataSize);
        using (this.lockObject.EnterScope())
        {
            if (file != 0 && this.fileToSize.TryGetValue(file, out var size))
            {
                var sizeDiff = dataSize - size;
                this.storageUsage += sizeDiff;

                this.fileToSize[file] = dataSize;

                if (sizeDiff != 0 && ((IStructuralObject)this).TryGetJournalWriter(out var root, out var writer, false))
                {
                    writer.Write(JournalRecordType.AddItem);
                    writer.Write(file);
                    writer.Write(dataSize);
                    writer.Write(sizeDiff);
                    root.AddJournalAndDispose(ref writer);
                }
            }
            else
            {// Not found
                file = this.CreateFileInternal(dataSize);
                this.storageUsage += dataSize;
            }
        }
    }

    /// <summary>
    /// Creates a unique file entry and includes its size in storage usage.
    /// </summary>
    /// <param name="size">The non-negative data size in bytes.</param>
    /// <returns>The new nonzero file identifier.</returns>
    public uint NewFileInternal(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        using (this.lockObject.EnterScope())
        {
            var file = this.CreateFileInternal(size);
            this.storageUsage += size;
            return file;
        }
    }

    public bool EqualsForTest(SimpleStorageData? other)
    {
        if (other is null)
        {
            return false;
        }

        if (this.storageUsage != other.storageUsage ||
            this.fileToSize.Count != other.fileToSize.Count)
        {
            return false;
        }

        foreach (var x in this.fileToSize)
        {
            if (!other.fileToSize.TryGetValue(x.Key, out var size) ||
                size != x.Value)
            {
                return false;
            }
        }

        return true;
    }

    bool ITinyhandCustomJournal.ReadCustomRecord(ref TinyhandReader reader)
    {
        if (!reader.TryReadJournalRecord(out var record))
        {
            return false;
        }

        using (this.lockObject.EnterScope())
        {
            if (record == JournalRecordType.AddItem)
            {
                var file = reader.ReadUInt32();
                var size = reader.ReadInt32();
                reader.ReadInt32(); // Legacy size delta; derive usage from the current index for idempotent replay.
                if (file == 0 || size < 0)
                {
                    return false;
                }

                this.fileToSize.TryGetValue(file, out var previousSize);
                this.fileToSize[file] = size;
                this.storageUsage += (long)size - previousSize;
                return true;
            }
            else if (record == JournalRecordType.DeleteItem)
            {
                var file = reader.ReadUInt32();
                this.TryRemoveFile(file);

                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint CreateFileInternal(int size)
    {// this.syncObject
        while (true)
        {
            var file = RandomVault.Default.NextUInt32();
            if (file != 0 && this.fileToSize.TryAdd(file, size))
            {
                if (((IStructuralObject)this).TryGetJournalWriter(out var root, out var writer, false))
                {
                    writer.Write(JournalRecordType.AddItem);
                    writer.Write(file);
                    writer.Write(size);
                    writer.Write(size);
                    root.AddJournalAndDispose(ref writer);
                }

                return file;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryRemoveFile(uint file)
    {
        if (this.fileToSize.Remove(file, out var size))
        {
            this.storageUsage -= size;
            return true;
        }
        else
        {// Not found
            return false;
        }
    }
}
