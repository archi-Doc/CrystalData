// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Arc;

namespace CrystalData;

/// <summary>
/// Identifies stored data by journal position, file identifier, and content hash.
/// </summary>
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
        try
        {
            var byteArray = Base32Sort.Default.FromStringToByteArray(base32);
            return TryRead(byteArray, out storageId);
        }
        catch
        {
            storageId = default;
            return false;
        }
    }

    public static bool TryRead(ReadOnlySpan<byte> span, out StorageId storageId)
    {
        if (span.Length < Length)
        {
            storageId = default;
            return false;
        }

        var journalPosition = BinaryPrimitives.ReadUInt64BigEndian(span);
        var fileId = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(sizeof(ulong)));
        var hash = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(sizeof(ulong) * 2));
        storageId = new(journalPosition, fileId, hash);
        return true;
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

        return Base32Sort.Default.FromBytesToString(span);
    }

    public override string ToString()
        => $"Position: {this.JournalPosition}, File id: {this.FileId}";

    public override bool Equals(object? obj)
        => obj is StorageId other && this.Equals(other);

    public bool Equals(StorageId other)
        => this.JournalPosition == other.JournalPosition &&
        this.FileId == other.FileId &&
        this.Hash == other.Hash;

    public bool Equals(ref StorageId other)
        => this.JournalPosition == other.JournalPosition &&
        this.FileId == other.FileId &&
        this.Hash == other.Hash;

    public static bool operator >(StorageId left, StorageId right)
        => left.CompareTo(right) > 0;

    public static bool operator <(StorageId left, StorageId right)
        => left.CompareTo(right) < 0;

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
        => HashCode.Combine(this.JournalPosition, this.FileId, this.Hash);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteSpan(Span<byte> span)
    {// The journal position is big-endian so that encoded names sort by position.
        BinaryPrimitives.WriteUInt64BigEndian(span, this.JournalPosition);
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(sizeof(ulong)), this.FileId);
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(sizeof(ulong) * 2), this.Hash);
    }
}
