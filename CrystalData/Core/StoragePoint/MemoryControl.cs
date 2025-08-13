// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1202

namespace CrystalData;

public partial class MemoryControl
{
    private const int MinimumDataSize = 1024;
    private const int UnloadIntervalInMilliseconds = 1_000;
    private const double StatRatio = 0.9d;
    private const double StatSumRev = (1 - StatRatio) / StatRatio;

    internal MemoryControl(Crystalizer crystalizer, Stat.GoshujinClass memoryStats)
    {
        this.crystalizer = crystalizer;
        this.unloader = new(this);
        this.unloader.Start();
        this.stats = memoryStats;
    }

    #region Unloader

    private class Unloader : TaskCore
    {
        public Unloader(MemoryControl memoryControl)
            : base(null, Process, false)
        {
            this.memoryControl = memoryControl;
        }

        private static async Task Process(object? parameter)
        {
            var core = (Unloader)parameter!;
            var memoryControl = core.memoryControl;
            var crystalizer = core.memoryControl.crystalizer;

            while (!core.IsTerminated)
            {
                if (memoryControl.MemoryUsage < StorageControl.Default.MemoryUsageLimit)
                {// Sleep
                    await core.Delay(UnloadIntervalInMilliseconds);
                    continue;
                }

                IStorageData? storageData;
                using (memoryControl.lockObject.EnterScope())
                {// Get the first item.
                    if (memoryControl.items.UnloadQueueChain.TryPeek(out var item))
                    {
                        memoryControl.items.UnloadQueueChain.Remove(item);
                        memoryControl.items.UnloadQueueChain.Enqueue(item);
                        storageData = item.StorageData;
                    }
                    else
                    {// No item
                        storageData = null;
                    }
                }

                if (storageData is null)
                {// Sleep
                    await core.Delay(UnloadIntervalInMilliseconds);
                    continue;
                }

                if (await storageData.Save(UnloadMode.TryUnload))
                {// Success (deletion will be done via ReportUnload() from StorageData)
                }
                else
                {// Failure
                }
            }
        }

        private readonly MemoryControl memoryControl;
    }

    #endregion

    [ValueLinkObject]
    [TinyhandObject]
    internal partial class Stat
    {
        public Stat()
        {
        }

        public Stat(int typeHash)
        {
            this.TypeHash = typeHash;
        }

        [Key(0)]
        [Link(Primary = true, Type = ChainType.Unordered)]
        public int TypeHash { get; private set; }

        [Key(1)]
        public double Accumulation { get; private set; }

        public int EstimatedSize
            => (int)(this.Accumulation * StatSumRev);

        public void Add(int dataSize)
            => this.Accumulation = (this.Accumulation * StatRatio) + (dataSize * (1 - StatRatio));
    }

    [ValueLinkObject]
    private partial class Item
    {
        [Link(Name = "UnloadQueue", Type = ChainType.QueueList)]
        public Item(IStorageData storageData, int dataSize)
        {
            this.StorageData = storageData;
            this.DataSize = dataSize;
        }

        [Link(Type = ChainType.Unordered)]
        public IStorageData StorageData { get; private set; }

        public int DataSize { get; set; }
    }

    #region FieldAndProperty

    internal bool IsActive { get; set; } = true;

    private readonly Crystalizer crystalizer;
    private readonly Unloader unloader;

    // Items/Stats
    private readonly Lock lockObject = new();
    private readonly Item.GoshujinClass items = new();
    private readonly Stat.GoshujinClass stats;
    private long memoryUsage;

    #endregion

    public long MemoryUsage => Volatile.Read(ref this.memoryUsage);

    public (long MemoryUsage, int MemoryCount) GetStat()
    {
        using (this.lockObject.EnterScope())
        {
            return (this.memoryUsage, this.items.UnloadQueueChain.Count);
        }
    }

    public void ReportUnloaded(IStorageData storageData, int dataSize)
    {
        if (!this.IsActive)
        {
            return;
        }

        using (this.lockObject.EnterScope())
        {
            if (this.items.StorageDataChain.FindFirst(storageData) is { } item)
            {
                this.memoryUsage -= item.DataSize;
                item.Goshujin = null;
            }

            var typeHash = storageData.DataType.GetHashCode();
            if (this.stats.TypeHashChain.FindFirst(typeHash) is not { } stat)
            {
                stat = new(typeHash);
                stat.Goshujin = this.stats;
            }

            stat.Add(dataSize);
        }
    }

    public void Register(IStorageData storageData, int dataSize)
    {
        if (!this.IsActive)
        {
            return;
        }

        using (this.lockObject.EnterScope())
        {
            if (dataSize <= 0)
            {// Estimate the data size.
                if (this.stats.TypeHashChain.FindFirst(storageData.DataType.GetHashCode()) is { } stat)
                {
                    dataSize = stat.EstimatedSize;
                }
            }

            dataSize = dataSize > MinimumDataSize ? dataSize : MinimumDataSize;

            if (this.items.StorageDataChain.FindFirst(storageData) is not { } item)
            {
                item = new(storageData, dataSize);
                item.Goshujin = this.items;
            }
            else
            {
                this.items.UnloadQueueChain.Remove(item);
                this.items.UnloadQueueChain.Enqueue(item);
                this.memoryUsage -= item.DataSize;
            }

            item.DataSize = dataSize;
            this.memoryUsage += item.DataSize;
        }
    }
}
