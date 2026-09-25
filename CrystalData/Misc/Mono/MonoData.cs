// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

#pragma warning disable SA1401 // Fields should be private

using Tinyhand.IO;

namespace CrystalData;

/// <summary>
/// Provides a thread-safe, Tinyhand-serializable key-value store with oldest-entry eviction.
/// </summary>
/// <typeparam name="TIdentifier">The key type.</typeparam>
/// <typeparam name="TDatum">The value type.</typeparam>
[TinyhandObject]
public partial class MonoData<TIdentifier, TDatum> : IMonoData<TIdentifier, TDatum>, ITinyhandSerializable<MonoData<TIdentifier, TDatum>>, ITinyhandSingleLayoutSerializable
{
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

        [Link(Type = ChainType.Unordered)]
        internal TIdentifier Key = default!;

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
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        this.capacity = capacity;
    }

    static void ITinyhandSerializable<MonoData<TIdentifier, TDatum>>.Serialize(ref TinyhandWriter writer, scoped ref MonoData<TIdentifier, TDatum>? value, TinyhandSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        using (value.goshujin.LockObject.EnterScope())
        {
            writer.WriteArrayHeader(2);
            writer.Write(value.capacity);
            writer.WriteArrayHeader(value.goshujin.QueueChain.Count);
            foreach (var item in value.goshujin.QueueChain)
            {
                writer.WriteArrayHeader(2);
                TinyhandSerializer.Serialize(ref writer, item.Key, options);
                TinyhandSerializer.Serialize(ref writer, item.Datum, options);
            }
        }
    }

    static void ITinyhandSerializable<MonoData<TIdentifier, TDatum>>.Deserialize(ref TinyhandReader reader, scoped ref MonoData<TIdentifier, TDatum>? value, TinyhandSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return;
        }

        value ??= new();
        var start = reader.Fork();
        Item.GoshujinClass? g = default;
        var capacity = 0;
        try
        {
            var length = reader.ReadArrayHeader();
            if (length >= 2)
            {
                capacity = reader.ReadInt32();
                ArgumentOutOfRangeException.ThrowIfNegative(capacity);
                var count = reader.ReadArrayHeader();
                g = new();
                for (var i = 0; i < count; i++)
                {
                    var itemLength = reader.ReadArrayHeader();
                    if (itemLength < 2)
                    {
                        g = null;
                        break;
                    }

                    var key = TinyhandSerializer.Deserialize<TIdentifier>(ref reader, options)!;
                    var datum = TinyhandSerializer.Deserialize<TDatum>(ref reader, options)!;
                    g.Add(new(key, datum));
                    while (itemLength-- > 2)
                    {// Unknown elements
                        reader.Skip();
                    }
                }

                while (g is not null && length-- > 2)
                {// Unknown elements
                    reader.Skip();
                }
            }
        }
        catch
        {
            g = null;
        }

        if (g is null)
        {// Invalid data: the whole value is skipped, so that the data after this value is read correctly.
            reader = start;
            reader.Skip();
            return;
        }

        value.goshujin = g;
        Volatile.Write(ref value.capacity, capacity);
    }

    /// <summary>
    /// Gets the number of items in the MonoData collection.
    /// </summary>
    public int Count
    {
        get
        {
            using (this.goshujin.LockObject.EnterScope())
            {
                return this.goshujin.QueueChain.Count;
            }
        }
    }

    /// <summary>
    /// Gets the capacity of the MonoData collection.
    /// </summary>
    [IgnoreMember]
    public int Capacity => Volatile.Read(ref this.capacity);

    [IgnoreMember]
    private Item.GoshujinClass goshujin = new();

    [IgnoreMember]
    private int capacity;

    /// <summary>
    /// Sets the capacity of the MonoData collection.
    /// </summary>
    /// <param name="capacity">The new capacity of the MonoData collection.</param>
    public void SetCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        using (this.goshujin.LockObject.EnterScope())
        {
            Volatile.Write(ref this.capacity, capacity);
            while (this.goshujin.QueueChain.Count > capacity)
            {
                this.goshujin.QueueChain.Dequeue().Goshujin = null;
            }
        }
    }

    /// <summary>
    /// Sets the specified identifier and data in the MonoData collection.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="value">The data associated with the identifier.</param>
    public void Set(in TIdentifier id, in TDatum value)
    {
        using (this.goshujin.LockObject.EnterScope())
        {
            if (this.goshujin.KeyChain.TryGetValue(id, out var item))
            {// Update
                item.Datum = value;
                this.goshujin.QueueChain.Remove(item);
                this.goshujin.QueueChain.Enqueue(item);
            }
            else
            {// New
                item = new Item(id, value);
                this.goshujin.Add(item);

                if (this.goshujin.QueueChain.Count > this.capacity)
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
    /// <param name="value">When this method returns, contains the data associated with the specified identifier, if the identifier is found; otherwise, the default value for the data type.</param>
    /// <returns><c>true</c> if the identifier is found in the MonoData collection; otherwise, <c>false</c>.</returns>
    public bool TryGet(in TIdentifier id, out TDatum value)
    {
        using (this.goshujin.LockObject.EnterScope())
        {
            if (this.goshujin.KeyChain.TryGetValue(id, out var item))
            {// Get
                value = item.Datum;
                return true;
            }
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Removes the specified identifier from the MonoData collection.
    /// </summary>
    /// <param name="id">The identifier key.</param>
    /// <returns><c>true</c> if the identifier is successfully removed; otherwise, <c>false</c>. This method also returns <c>false</c> if the identifier was not found in the MonoData collection.</returns>
    public bool Remove(in TIdentifier id)
    {
        using (this.goshujin.LockObject.EnterScope())
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
