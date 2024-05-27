// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/*public interface ISimpleSerializable
{
    void Serialize(ref Tinyhand.IO.TinyhandWriter writer);

    bool Deserialize(ReadOnlySpan<byte> span, out int bytesRead);
}*/

public static class SerializeHelper
{
    // public const int StandardFragmentSize = 1024 * 4; // 4KB

    // public static TinyhandSerializerOptions SerializerOptions { get; } = TinyhandSerializerOptions.Standard;

    public static (TData? Data, SaveFormat Format) TryDeserialize<TData>(ReadOnlySpan<byte> span, SaveFormat formatHint, bool reconstructIfEmpty)
        where TData : ITinyhandSerialize<TData>, ITinyhandReconstruct<TData>
    {
        TData? data = default;
        SaveFormat format = SaveFormat.Binary;

        if (span.Length == 0)
        {// Empty
            if (reconstructIfEmpty)
            {
                data = TinyhandSerializer.ReconstructObject<TData>();
            }

            return (data, format);
        }

        if (formatHint == SaveFormat.Utf8)
        {// utf8
            try
            {
                TinyhandSerializer.DeserializeObjectFromUtf8(span, ref data);
                format = SaveFormat.Utf8;
            }
            catch
            {// Maybe binary...
                data = default;
                try
                {
                    TinyhandSerializer.DeserializeObject(span, ref data);
                }
                catch
                {
                    data = default;
                }
            }
        }
        else
        {// Binary
            try
            {
                TinyhandSerializer.DeserializeObject(span, ref data);
            }
            catch
            {// Maybe utf8...
                data = default;
                try
                {
                    TinyhandSerializer.DeserializeObjectFromUtf8(span, ref data);
                    format = SaveFormat.Utf8;
                }
                catch
                {
                    data = default;
                }
            }
        }

        return (data, format);
    }

    public static T? TryReadAndDeserialize<T>(string path)
        where T : ITinyhandSerialize<T>
    {
        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch
        {
            return default;
        }

        try
        {
            return TinyhandSerializer.DeserializeObjectFromUtf8<T>(data);
        }
        catch
        {
            return default;
        }
    }

    public static async Task<bool> TrySerializeAndWrite<T>(T obj, string path)
        where T : ITinyhandSerialize<T>
    {
        try
        {
            var bytes = TinyhandSerializer.SerializeToUtf8(obj);
            await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /*public static bool TrySerialize<T>(T obj, out BytePool.RentMemory rentMemory)
        where T : ITinyhandSerialize<T>
    {
        var arrayOwner = BytePool.Default.Rent(StandardFragmentSize);
        try
        {
            var writer = new Tinyhand.IO.TinyhandWriter(arrayOwner.Array);
            TinyhandSerializer.SerializeObject(ref writer, obj, SerializerOptions);

            writer.FlushAndGetArray(out var array, out var arrayLength, out var isInitialBuffer);
            if (isInitialBuffer)
            {
                rentMemory = arrayOwner.AsMemory(0, arrayLength);
                return true;
            }
            else
            {
                arrayOwner.Return();
                rentMemory = new BytePool.RentMemory(array);
                return true;
            }
        }
        catch
        {
            arrayOwner.Return();
            rentMemory = default;
            return false;
        }
    }

    public static bool Serialize<T>(T obj, TinyhandSerializerOptions options, out BytePool.RentMemory rentMemory)
    {
        var arrayOwner = BytePool.Default.Rent(StandardFragmentSize);
        try
        {
            var writer = new Tinyhand.IO.TinyhandWriter(arrayOwner.Array);
            TinyhandSerializer.Serialize(ref writer, obj, options);

            writer.FlushAndGetArray(out var array, out var arrayLength, out var isInitialBuffer);
            if (isInitialBuffer)
            {
                rentMemory = arrayOwner.AsMemory(0, arrayLength);
                return true;
            }
            else
            {
                arrayOwner.Return();
                rentMemory = new BytePool.RentMemory(array);
                return true;
            }
        }
        catch
        {
            arrayOwner.Return();
            rentMemory = default;
            return false;
        }
    }*/

    /*public static byte[] Serialize(Dictionary<ulong, ISimpleSerializable> dictionary)
    {
        var writer = default(Tinyhand.IO.TinyhandWriter);
        byte[]? byteArray;
        try
        {
            foreach (var x in dictionary)
            {
                var span = writer.GetSpan(12); // Id + Length
                writer.Advance(12);

                var written = writer.Written;
                x.Value.Serialize(ref writer);

                BitConverter.TryWriteBytes(span, x.Key); // Id
                span = span.Slice(8);
                BitConverter.TryWriteBytes(span, (int)(writer.Written - written)); // Length
            }

            byteArray = writer.FlushAndGetArray();
        }
        finally
        {
            writer.Dispose();
        }

        return byteArray;
    }

    public static bool Deserialize(Dictionary<ulong, ISimpleSerializable> dictionary, ReadOnlySpan<byte> span)
    {
        try
        {
            while (span.Length >= 12)
            {
                var id = BitConverter.ToUInt64(span); // Id
                span = span.Slice(8);
                var length = BitConverter.ToInt32(span); // Length
                span = span.Slice(4);

                if (dictionary.TryGetValue(id, out var x))
                {
                    x.Deserialize(span, out _);
                }

                span = span.Slice(length);
            }
        }
        catch
        {
            return false;
        }

        return true;
    }*/
}
