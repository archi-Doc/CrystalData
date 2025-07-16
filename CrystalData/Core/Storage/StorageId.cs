// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace CrystalData;

[TinyhandObject]
public readonly partial struct StorageId : IEquatable<StorageId>, IComparable<StorageId>
{// StorageId: JournalPosition 8 bytes, File id 8 bytes, Hash 8 bytes
    public const string Extension = "storage";
    public const int Length = 24; // 8 + 8 + 8
    public static readonly StorageId Invalid = default;
    public static readonly StorageId Empty = new(1, 0, 0);
    public static readonly int LengthInBase32;

    #region FieldAndProperty

    [Key(0)]
    public readonly ulong JournalPosition;

    [Key(1)]
    public readonly ulong FileId;

    [Key(2)]
    public readonly ulong Hash;

    public bool IsValid => this.JournalPosition != 0;

    #endregion

    static StorageId()
    {
        LengthInBase32 = Base32Sort.GetEncodedLength(Length);
    }

    public StorageId()
    {
    }

    public StorageId(ulong journalPosition, ulong fileId, ulong hash)
    {
        this.JournalPosition = journalPosition;
        this.FileId = fileId;
        this.Hash = hash;
    }

    public static bool TryParse(string base32, out StorageId storageId)
    {
        var byteArray = Base32Sort.Default.FromStringToByteArray(base32);
        return TryParse(byteArray, out storageId);
    }

    public static bool TryParse(ReadOnlySpan<byte> span, out StorageId storageId)
    {
        if (span.Length >= Length)
        {
            try
            {
                var journalPosition = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt64(span));
                span = span.Slice(sizeof(ulong));
                var fileId = BitConverter.ToUInt64(span);
                span = span.Slice(sizeof(ulong));
                var hash = BitConverter.ToUInt64(span);
                storageId = new(journalPosition, fileId, hash);
                return true;
            }
            catch
            {
            }
        }

        storageId = default;
        return false;
    }

    public byte[] ToByteArray()
    {
        var byteArray = new byte[Length];
        this.WriteSpan(byteArray.AsSpan());

        return byteArray;
    }

    public string ToBase32()
    {
        Span<byte> span = stackalloc byte[Length];
        this.WriteSpan(span);

        return Base32Sort.Default.FromByteArrayToString(span);
    }

    public override string ToString()
        => $"Position: {this.JournalPosition}, File id: {this.FileId}";

    public bool Equals(StorageId other)
        => this.JournalPosition == other.JournalPosition &&
        this.FileId == other.FileId &&
        this.Hash == other.Hash;

    public static bool operator >(StorageId w1, StorageId w2)
        => w1.CompareTo(w2) > 0;

    public static bool operator <(StorageId w1, StorageId w2)
        => w1.CompareTo(w2) < 0;

    public int CompareTo(StorageId other)
    {
        var cmp = this.JournalPosition.CircularCompareTo(other.JournalPosition);
        if (cmp != 0)
        {
            return cmp;
        }

        if (this.FileId < other.FileId)
        {
            return -1;
        }
        else if (this.FileId > other.FileId)
        {
            return 1;
        }

        if (this.Hash < other.Hash)
        {
            return -1;
        }
        else if (this.Hash > other.Hash)
        {
            return 1;
        }

        return 0;
    }

    public override int GetHashCode()
        => HashCode.Combine(this.JournalPosition, this.Hash);

    private static void WriteBigEndian(ulong value, Span<byte> span)
    {
        unchecked
        {
            // Write to highest index first so the JIT skips bounds checks on subsequent writes.
            span[7] = (byte)value;
            span[6] = (byte)(value >> 8);
            span[5] = (byte)(value >> 16);
            span[4] = (byte)(value >> 24);
            span[3] = (byte)(value >> 32);
            span[2] = (byte)(value >> 40);
            span[1] = (byte)(value >> 48);
            span[0] = (byte)(value >> 56);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteSpan(Span<byte> span)
    {
        WriteBigEndian(this.JournalPosition, span);
        span = span.Slice(sizeof(ulong));
        BitConverter.TryWriteBytes(span, this.Hash);
    }
}
