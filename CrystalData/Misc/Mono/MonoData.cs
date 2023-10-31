// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1401 // Fields should be private

using Tinyhand.IO;

namespace CrystalData;

[TinyhandObject]
public partial class MonoData<TIdentifier, TDatum> : IMonoData<TIdentifier, TDatum>, ITinyhandSerialize<MonoData<TIdentifier, TDatum>>
{
    [TinyhandObject]
    [ValueLinkObject(Isolation = IsolationLevel.Serializable)]
    private sealed partial class Item
    {
        [Link(Primary = true, Name = "Queue", Type = ChainType.QueueList)]
        public Item(TIdentifier key, TDatum datum)
        {
            this.Key = key;
            this.Datum = datum;
        }

        public Item()
        {
        }

        [Key(0)]
        [Link(Type = ChainType.Unordered)]
        internal TIdentifier Key = default!;

        [Key(1)]
        internal TDatum Datum = default!;
    }

    public MonoData()
    {
    }

    public MonoData(int capacity)
    {
        this.Capacity = capacity;
    }

    static void ITinyhandSerialize<MonoData<TIdentifier, TDatum>>.Serialize(ref TinyhandWriter writer, scoped ref MonoData<TIdentifier, TDatum>? value, TinyhandSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        TinyhandSerializer.Serialize(ref writer, value.goshujin, options);
    }

    static void ITinyhandSerialize<MonoData<TIdentifier, TDatum>>.Deserialize(ref TinyhandReader reader, scoped ref MonoData<TIdentifier, TDatum>? value, TinyhandSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return;
        }

        value ??= new();
        Item.GoshujinClass? g = default;
        try
        {
            g = TinyhandSerializer.Deserialize<Item.GoshujinClass>(ref reader, options);
        }
        catch
        {
        }

        if (g is not null)
        {
            value.goshujin = g;
        }
    }

    [IgnoreMember]
    public int Capacity { get; private set; }

    [IgnoreMember]
    private Item.GoshujinClass goshujin = new();

    public void Set(in TIdentifier id, in TDatum datum)
    {
        lock (this.goshujin.SyncObject)
        {
            if (this.goshujin.KeyChain.TryGetValue(id, out var item))
            {// Update
                item.Datum = datum;
                this.goshujin.QueueChain.Remove(item);
                this.goshujin.QueueChain.Enqueue(item);
            }
            else
            {// New
                item = new Item(id, datum);
                this.goshujin.Add(item);

                if (this.goshujin.QueueChain.Count > this.Capacity)
                {// Remove the oldest item;
                    this.goshujin.QueueChain.Dequeue().Goshujin = null;
                }
            }
        }
    }

    public bool TryGet(in TIdentifier id, out TDatum datum)
    {
        lock (this.goshujin.SyncObject)
        {
            if (this.goshujin.KeyChain.TryGetValue(id, out var item))
            {// Get
                datum = item.Datum;
                return true;
            }
        }

        datum = default!;
        return false;
    }

    public bool Remove(in TIdentifier id)
    {
        lock (this.goshujin.SyncObject)
        {
            if (this.goshujin.KeyChain.TryGetValue(id, out var item))
            {
                item.Goshujin = null;
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public void SetCapacity(int capacity)
    {
        this.Capacity = capacity;
    }

    public int Count()
    {
        lock (this.goshujin.SyncObject)
        {
            return this.goshujin.QueueChain.Count;
        }
    }
}
