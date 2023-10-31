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
        var byteArray = Base32Sort.Default.FromStringToByteArray(base32);
        return TryParse(byteArray, out bookTitle);
    }

    public static bool TryParse(ReadOnlySpan<byte> span, out BookTitle bookTitle)
    {
        if (span.Length >= Length)
        {
            try
            {
                var journalPosition = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt64(span));
                span = span.Slice(sizeof(ulong));
                var hash = BitConverter.ToUInt64(span);
                span = span.Slice(sizeof(ulong));
                var reserved = BitConverter.ToUInt32(span);

                bookTitle = new(journalPosition, hash);
                return true;
            }
            catch
            {
            }
        }

        bookTitle = default;
        return false;
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

        return Base32Sort.Default.FromByteArrayToString(span);
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
        span = span.Slice(sizeof(ulong));
        BitConverter.TryWriteBytes(span, this.Reserved);
    }
}
