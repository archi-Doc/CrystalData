// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

using System.Diagnostics.CodeAnalysis;
using CrystalData.Internal;

namespace CrystalData;

[TinyhandObject]
public sealed partial class StorageMap
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
}
