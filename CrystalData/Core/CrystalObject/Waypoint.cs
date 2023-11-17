// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace CrystalData;

[TinyhandObject]
public readonly partial struct Waypoint : IEquatable<Waypoint>, IComparable<Waypoint>
{// JournalPosition, Plane, Hash
    public const string Extension = "waypoint";
    public const int Length = 24; // 8 + 4 + 8 + 4
    public const ulong InvalidJournalPosition = 0;
    public const ulong ValidJournalPosition = 1;
    public static readonly Waypoint Invalid = default;
    public static readonly Waypoint Empty = new(1, 0, 0);
    public static readonly int LengthInBase32;

    static Waypoint()
    {
        LengthInBase32 = Base32Sort.GetEncodedLength(Length);
    }

    public Waypoint()
    {
    }

    public Waypoint(ulong journalPosition, uint plane, ulong hash)
    {
        this.JournalPosition = journalPosition;
        this.Plane = plane;
        this.Hash = hash;
        this.Reserved = 0;
    }

    public static bool TryParse(string base32, out Waypoint waypoint)
    {
        var byteArray = Base32Sort.Default.FromStringToByteArray(base32);
        return TryRead(byteArray, out waypoint);
    }

    public static bool TryRead(ReadOnlySpan<byte> span, out Waypoint waypoint)
    {
        if (span.Length >= Length)
        {
            try
            {
                var journalPosition = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt64(span));
                span = span.Slice(sizeof(ulong));
                var plane = BitConverter.ToUInt32(span);
                span = span.Slice(sizeof(uint));
                var hash = BitConverter.ToUInt64(span);
                waypoint = new(journalPosition, plane, hash);
                return true;
            }
            catch
            {
            }
        }

        waypoint = default;
        return false;
    }

    [Key(0)]
    public readonly ulong JournalPosition;

    [Key(1)]
    public readonly uint Plane; // Où allons-nous

    [Key(2)]
    public readonly ulong Hash;

    [Key(3)]
    public readonly uint Reserved;

    public bool IsValid => this.JournalPosition != 0;

    public Waypoint WithHash(ulong hash)
        => new(this.JournalPosition, this.Plane, hash);

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
        => $"Position: {this.JournalPosition}, Plane: {this.Plane}";

    public bool Equals(Waypoint other)
        => this.JournalPosition == other.JournalPosition &&
        this.Plane == other.Plane &&
        this.Hash == other.Hash;

    public static bool operator >(Waypoint w1, Waypoint w2)
        => w1.CompareTo(w2) > 0;

    public static bool operator <(Waypoint w1, Waypoint w2)
        => w1.CompareTo(w2) < 0;

    public int CompareTo(Waypoint other)
    {
        var cmp = this.JournalPosition.CircularCompareTo(other.JournalPosition);
        if (cmp != 0)
        {
            return cmp;
        }

        if (this.Plane < other.Plane)
        {
            return -1;
        }
        else if (this.Plane > other.Plane)
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
        => HashCode.Combine(this.JournalPosition, this.Plane, this.Hash);

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
        // BitConverter.TryWriteBytes(span, this.JournalPosition);
        WriteBigEndian(this.JournalPosition, span);
        span = span.Slice(sizeof(ulong));
        BitConverter.TryWriteBytes(span, this.Plane);
        span = span.Slice(sizeof(uint));
        BitConverter.TryWriteBytes(span, this.Hash);
        span = span.Slice(sizeof(ulong));
        BitConverter.TryWriteBytes(span, this.Reserved);
    }
}
