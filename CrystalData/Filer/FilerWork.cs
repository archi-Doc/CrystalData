// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Filer;

public class FilerWork : IEquatable<FilerWork>
{
    public enum WorkType
    {
        Write,
        Read,
        Delete,
        DeleteDirectory,
        List,
    }

    public WorkType Type { get; }

    public CrystalResult Result { get; internal set; } = CrystalResult.NotStarted;

    public string Path { get; }

    public long Offset { get; }

    public int Length { get; }

    public bool Truncate { get; }

    public ByteArrayPool.ReadOnlyMemoryOwner WriteData { get; }

    public ByteArrayPool.MemoryOwner ReadData { get; internal set; }

    public object? OutputObject { get; internal set; }

    public FilerWork(string path, long offset, ByteArrayPool.ReadOnlyMemoryOwner dataToBeShared, bool truncate)
    {// Write
        this.Type = WorkType.Write;
        this.Path = path;
        this.Offset = offset;
        this.Truncate = truncate;
        this.WriteData = dataToBeShared.IncrementAndShare();
    }

    public FilerWork(string path, long offset, int length)
    {// Read
        this.Type = WorkType.Read;
        this.Path = path;
        this.Offset = offset;
        this.Length = length;
    }

    public FilerWork(WorkType workType, string path)
    {// Delete/List
        this.Type = workType;
        this.Path = path;
    }

    public override int GetHashCode()
        => HashCode.Combine(this.Type, this.Path, this.WriteData.Memory.Length, this.Length);

    public bool Equals(FilerWork? other)
    {
        if (other == null)
        {
            return false;
        }

        return this.Type == other.Type &&
            this.Path == other.Path &&
            this.WriteData.Memory.Span.SequenceEqual(other.WriteData.Memory.Span) &&
            this.Length == other.Length;
    }

    public override string ToString()
        => $"{this.Type.ToString()}:{this.Path}";
}
