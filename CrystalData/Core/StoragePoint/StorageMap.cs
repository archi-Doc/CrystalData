// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

using System.Buffers;
using CrystalData.Internal;
using Tinyhand.IO;

namespace CrystalData;

[TinyhandObject(UseServiceProvider = true, ExplicitKeyOnly = true)]
public sealed partial class StorageMap : IStructualObject
{
    public const string Filename = "Map";
    private const int StoreBatchSize = 100;

    public static readonly StorageMap Disabled = new();

    #region FiendAndProperty

    public StorageControl StorageControl { get; private set; }

    private bool enabledStorageMap;

    [Key(0)]
    private StorageObject.GoshujinClass storageObjects = new(); // Lock:StorageControl

    private long storageUsage;

    internal StorageObject.GoshujinClass StorageObjects => this.storageObjects; // Lock:StorageControl

    public bool IsEnabled => this.enabledStorageMap;

    public long StorageUsage => this.storageUsage;

    #endregion

    public StorageMap()
    {
        this.StorageControl = StorageControl.Disabled;
        this.enabledStorageMap = false;
    }

    /*
    #region IStructualRoot

    bool IStructualRoot.TryGetJournalWriter(JournalType recordType, out TinyhandWriter writer)
    {
        if (this.crystalizer.Journal is not null)
        {
            this.crystalizer.Journal.GetWriter(recordType, out writer);

            writer.Write_Locator();
            writer.Write(this.);
            return true;
        }
        else
        {
            writer = default;
            return false;
        }
    }

    ulong IStructualRoot.AddJournal(ref TinyhandWriter writer)
    {
        if (this.crystalizer.Journal is not null)
        {
            return this.crystalizer.Journal.Add(ref writer);
        }
        else
        {
            return 0;
        }
    }

    bool IStructualRoot.TryAddToSaveQueue()
    {
        if (this.CrystalConfiguration.SavePolicy == SavePolicy.OnChanged)
        {
            this.Crystalizer.AddToSaveQueue(this);
            return true;
        }
        else
        {
            return false;
        }
    }

    #endregion*/

    #region IStructualObject

    IStructualRoot? IStructualObject.StructualRoot { get; set; }

    IStructualObject? IStructualObject.StructualParent { get; set; }

    int IStructualObject.StructualKey { get; set; }

    bool IStructualObject.ReadRecord(ref TinyhandReader reader)
    {
        if (!reader.TryReadJournalRecord(out JournalRecord record))
        {
            return false;
        }

        if (record == JournalRecord.AddItem)
        {
            var pointId = reader.ReadUInt64();
            var typeIdentifier = reader.ReadUInt32();
            if (!this.storageObjects.PointIdChain.TryGetValue(pointId, out var storageObject))
            {
                storageObject = new();
                storageObject.Initialize(pointId, typeIdentifier, this);
                storageObject.Goshujin = this.StorageObjects;
            }

            return true;
        }
        else if (record == JournalRecord.Locator)
        {
            var pointId = reader.ReadUInt64();
            if (this.storageObjects.PointIdChain.TryGetValue(pointId, out var storageObject))
            {
                return ((IStructualObject)storageObject).ReadRecord(ref reader);
            }
        }

        return false;
    }

    void IStructualObject.WriteLocator(ref TinyhandWriter writer)
    {
    }

    #endregion

    internal void Enable(StorageControl storageControl)
    {
        this.StorageControl = storageControl;
        this.enabledStorageMap = true;
        storageControl.AddStorageMap(this);
    }

    private void UpdateStorageUsageInternal(long size)
        => this.storageUsage += size;

    [TinyhandOnDeserialized]
    private void OnDeserialized()
    {
        foreach (var x in this.StorageObjects)
        {
            x.storageMap = this;
        }
    }
}
