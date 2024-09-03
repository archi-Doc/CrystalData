// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1401 // Fields should be private

using Tinyhand.IO;

namespace CrystalData;

/// <summary>
/// <see cref="MonoData{TIdentifier, TDatum}"/> is a simple key-value store that uses TinyhandSerializer.<br/>
/// You can set the data capacity, and data exceeding this limit will be deleted in order from the oldest.<br/>
/// It is thread-safe (using lock statements).
/// </summary>
/// <typeparam name="TIdentifier">The type of the identifier.</typeparam>
/// <typeparam name="TDatum">The type of the data.</typeparam>
[TinyhandObject]
public partial class MonoData<TIdentifier, TDatum> : IMonoData<TIdentifier, TDatum>, ITinyhandSerialize<MonoData<TIdentifier, TDatum>>
{
    [TinyhandObject]
    [ValueLinkObject(Isolation = IsolationLevel.Serializable)]
    private sealed partial class Item
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Item"/> class.
        /// </summary>
        /// <param name="key">The identifier.</param>
        /// <param name="datum">The data associated with the key.</param>
        [Link(Primary = true, Name = "Queue", Type = ChainType.QueueList)]
        public Item(TIdentifier key, TDatum datum)
        {
            this.Key = key;
            this.Datum = datum;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Item"/> class.
        /// </summary>
        public Item()
        {
        }

        [Key(0)]
        [Link(Type = ChainType.Unordered)]
        internal TIdentifier Key = default!;

        [Key(1)]
        internal TDatum Datum = default!;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MonoData{TIdentifier, TDatum}"/> class.
    /// </summary>
    public MonoData()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MonoData{TIdentifier, TDatum}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity of the MonoData collection.</param>
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

    /// <summary>
    /// Gets the number of items in the MonoData collection.
    /// </summary>
    public int Count => this.goshujin.QueueChain.Count;

    /// <summary>
    /// Gets the capacity of the MonoData collection.
    /// </summary>
    [IgnoreMember]
    public int Capacity { get; private set; }

    [IgnoreMember]
    private Item.GoshujinClass goshujin = new();

    /// <summary>
    /// Sets the capacity of the MonoData collection.
    /// </summary>
    /// <param name="capacity">The new capacity of the MonoData collection.</param>
    public void SetCapacity(int capacity)
    {
        this.Capacity = capacity;
    }

    /// <summary>
    /// Sets the specified identifier and data in the MonoData collection.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="datum">The data associated with the identifier.</param>
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

    /// <summary>
    /// Tries to get the data associated with the specified identifier from the MonoData collection.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="datum">When this method returns, contains the data associated with the specified identifier, if the identifier is found; otherwise, the default value for the data type.</param>
    /// <returns><c>true</c> if the identifier is found in the MonoData collection; otherwise, <c>false</c>.</returns>
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

    /// <summary>
    /// Removes the specified identifier from the MonoData collection.
    /// </summary>
    /// <param name="id">The identifier key.</param>
    /// <returns><c>true</c> if the identifier is successfully removed; otherwise, <c>false</c>. This method also returns <c>false</c> if the identifier was not found in the MonoData collection.</returns>
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
}
