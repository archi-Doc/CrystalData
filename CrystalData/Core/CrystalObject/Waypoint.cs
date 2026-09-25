// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Arc;

namespace CrystalData;

/// <summary>
/// Identifies a crystal snapshot by journal position, content hash, and journal plane.
/// </summary>
[TinyhandObject]
public readonly partial struct Waypoint : IEquatable<Waypoint>, IComparable<Waypoint>
{// JournalPosition, Plane, Hash
    public const int Length = 24; // 8 + 8 + 4 + 4
    public const ulong InvalidJournalPosition = 0;
    public const ulong ValidJournalPosition = 1;
    public static readonly Waypoint Invalid = default;
    // public static readonly Waypoint Empty = new(1, 0, 0);
    public static readonly int LengthInBase32;

    static Waypoint()
    {
        LengthInBase32 = Base32Sort.GetEncodedLength(Length);
    }

    #region FieldAndProperty

    [Key(0)]
    public readonly ulong JournalPosition;

    [Key(1)]
    public readonly ulong Hash;

    [Key(2)]
    public readonly uint Plane; // Où allons-nous

    [Key(3)]
    public readonly uint Reserved;

    public bool IsValid => this.JournalPosition != 0;

    #endregion

    public Waypoint()
    {
    }

    public Waypoint(ulong journalPosition, ulong hash, uint plane)
    {
        this.JournalPosition = journalPosition;
        this.Hash = hash;
        this.Plane = plane;
        this.Reserved = 0;
    }

    public static bool TryParse(string base32, out Waypoint waypoint)
    {
        try
        {
            var byteArray = Base32Sort.Default.FromStringToByteArray(base32);
            return TryRead(byteArray, out waypoint);
        }
        catch
        {
            waypoint = default;
            return false;
        }
    }

    public static bool TryRead(ReadOnlySpan<byte> span, out Waypoint waypoint)
    {
        if (span.Length < Length)
        {
            waypoint = default;
            return false;
        }

        var journalPosition = BinaryPrimitives.ReadUInt64BigEndian(span);
        var hash = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(sizeof(ulong)));
        var plane = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(sizeof(ulong) * 2));
        waypoint = new(journalPosition, hash, plane);
        return true;
    }

    public Waypoint WithHash(ulong hash)
        => new(this.JournalPosition, hash, this.Plane);

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
        => $"Position: {this.JournalPosition}, Plane: {this.Plane}";

    public override bool Equals(object? obj)
        => obj is Waypoint other && this.Equals(other);

    public bool Equals(Waypoint other)
        => this.JournalPosition == other.JournalPosition &&
        this.Hash == other.Hash &&
        this.Plane == other.Plane;

    public bool Equals(ref Waypoint other)
        => this.JournalPosition == other.JournalPosition &&
        this.Hash == other.Hash &&
        this.Plane == other.Plane;

    public static bool operator >(Waypoint left, Waypoint right)
        => left.CompareTo(right) > 0;

    public static bool operator <(Waypoint left, Waypoint right)
        => left.CompareTo(right) < 0;

    public int CompareTo(Waypoint other)
    {
        var cmp = this.JournalPosition.CircularCompareTo(other.JournalPosition);
        if (cmp != 0)
        {
            return cmp;
        }

        if (this.Hash < other.Hash)
        {
            return -1;
        }
        else if (this.Hash > other.Hash)
        {
            return 1;
        }

        if (this.Plane < other.Plane)
        {
            return -1;
        }
        else if (this.Plane > other.Plane)
        {
            return 1;
        }

        return 0;
    }

    public override int GetHashCode()
        => HashCode.Combine(this.JournalPosition, this.Plane, this.Hash);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteSpan(Span<byte> span)
    {// The journal position is big-endian so that encoded names sort by position.
        BinaryPrimitives.WriteUInt64BigEndian(span, this.JournalPosition);
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(sizeof(ulong)), this.Hash);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(sizeof(ulong) * 2), this.Plane);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice((sizeof(ulong) * 2) + sizeof(uint)), this.Reserved);
    }
}
