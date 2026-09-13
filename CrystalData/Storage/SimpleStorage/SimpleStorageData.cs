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
            value.storageUsage = reader.ReadInt64();

            // 2nd item
            var count = reader.ReadMapHeaderOrEmptyArray();
            value.fileToSize = new(count);
            for (var i = 0; i < count; i++)
            {
                value.fileToSize.TryAdd(reader.ReadUInt32(), reader.ReadInt32());
            }
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

    public void Put(ref uint file, int dataSize)
    {
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
                file = this.NewFileInternal(dataSize);
                this.storageUsage += dataSize;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint NewFileInternal(int size)
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

        if (record == JournalRecordType.AddItem)
        {
            var file = reader.ReadUInt32();
            var size = reader.ReadInt32();
            var diff = reader.ReadInt32();
            this.fileToSize[file] = size;
            this.storageUsage += diff;

            return true;
        }
        else if (record == JournalRecordType.DeleteItem)
        {
            var file = reader.ReadUInt32();
            this.TryRemoveFile(file);

            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryRemoveFile(uint file)
    {
        if (this.fileToSize.TryGetValue(file, out var size))
        {
            this.fileToSize.Remove(file);
            this.storageUsage -= size;
            return true;
        }
        else
        {// Not found
            return false;
        }
    }
}
