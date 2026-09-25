// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace CrystalData.Journal;

internal readonly struct BookTitle : IEquatable<BookTitle>, IComparable<BookTitle>
{// JournalPosition, Hash, Reserved
    public const int Length = 20; // 8 + 8 + 4
    public static readonly BookTitle Invalid = default;
    public static readonly int LengthInBase32;

    static BookTitle()
    {
        LengthInBase32 = Base32Sort.GetEncodedLength(Length);
    }

    public BookTitle(ulong journalPosition, ulong hash)
    {
        this.JournalPosition = journalPosition;
        this.Hash = hash;
        this.Reserved = 0;
    }

    public static bool TryParse(string base32, out BookTitle bookTitle)
    {
        try
        {
            var byteArray = Base32Sort.Default.FromStringToByteArray(base32);
            return TryParse(byteArray, out bookTitle);
        }
        catch
        {
            bookTitle = default;
            return false;
        }
    }

    public static bool TryParse(ReadOnlySpan<byte> span, out BookTitle bookTitle)
    {
        if (span.Length < Length)
        {
            bookTitle = default;
            return false;
        }

        var journalPosition = BinaryPrimitives.ReadUInt64BigEndian(span);
        var hash = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(sizeof(ulong)));
        bookTitle = new(journalPosition, hash);
        return true;
    }

    public readonly ulong JournalPosition;
    public readonly ulong Hash;
    public readonly uint Reserved;

    public bool IsValid => this.JournalPosition != 0;

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

    public bool Equals(BookTitle other)
        => this.JournalPosition == other.JournalPosition &&
        this.Hash == other.Hash &&
        this.Reserved == other.Reserved;

    public int CompareTo(BookTitle other)
    {
        if (this.JournalPosition < other.JournalPosition)
        {
            return -1;
        }
        else if (this.JournalPosition > other.JournalPosition)
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

        if (this.Reserved < other.Reserved)
        {
            return -1;
        }
        else if (this.Reserved > other.Reserved)
        {
            return 1;
        }

        return 0;
    }

    public override int GetHashCode()
        => HashCode.Combine(this.JournalPosition, this.Hash, this.Reserved);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteSpan(Span<byte> span)
    {// The journal position is big-endian so that encoded names sort by position.
        BinaryPrimitives.WriteUInt64BigEndian(span, this.JournalPosition);
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(sizeof(ulong)), this.Hash);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(sizeof(ulong) * 2), this.Reserved);
    }
}
