// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Filer;

public sealed record class FilerWork : ReusableTaskJob, IEquatable<FilerWork>
{
    public enum WorkType
    {
        Write,
        Read,
        Delete,
        DeleteEmptyDirectory,
        DeleteDirectory,
        List,
    }

    public WorkType Type { get; private set; }

    public CrystalResult Result { get; internal set; } = CrystalResult.NotStarted;

    public string Path { get; private set; } = string.Empty;

    public long Offset { get; private set; }

    public int Length { get; private set; }

    public bool Truncate { get; private set; }

    public BytePool.RentReadOnlyMemory WriteData { get; private set; }

    public BytePool.RentMemory ReadData { get; internal set; }

    public object? OutputObject { get; internal set; }

    public FilerWork()
    {
    }

    public void Initialize(string path, long offset, BytePool.RentReadOnlyMemory dataToBeShared, bool truncate)
    {// Write
        this.Type = WorkType.Write;
        this.Path = path;
        this.Offset = offset;
        this.Truncate = truncate;
        this.WriteData = dataToBeShared.IncrementAndShare();
    }

    public void Initialize(string path, long offset, int length)
    {// Read
        this.Type = WorkType.Read;
        this.Path = path;
        this.Offset = offset;
        this.Length = length;
    }

    public void Initialize(WorkType workType, string path)
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

    public override void OnJobFinished()
    {
        this.wor
    }
}
