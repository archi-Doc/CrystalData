// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

using CrystalData.Internal;
using Tinyhand.IO;

namespace CrystalData;

[TinyhandObject(UseServiceProvider = true)]
public sealed partial class StorageMap : IStructualObject
{
    public const string Filename = "Map";

    #region FiendAndProperty

    public StorageControl StorageControl { get; }

    private bool enabledStorageMap = true;

    [Key(0)]
    private StorageObject.GoshujinClass storageObjects = new(); // Lock:StorageControl

    private long storageUsage;

    internal StorageObject.GoshujinClass StorageObjects => this.storageObjects; // Lock:StorageControl

    public bool IsEnabled => this.enabledStorageMap;

    public bool IsDisabled => !this.enabledStorageMap;

    public long StorageUsage => this.storageUsage;

    #endregion

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
        return false;
    }

    void IStructualObject.WriteLocator(ref TinyhandWriter writer)
    {
    }

    #endregion

    public StorageMap(StorageControl storageControl)
    {
        this.StorageControl = storageControl;
        storageControl.AddStorageMap(this);
    }

    internal void DisableStorageMap()
    {
        this.enabledStorageMap = false;
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
