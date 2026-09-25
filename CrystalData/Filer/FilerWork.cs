// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Filer;

/// <summary>
/// Describes one queued read, write, delete, or list operation for a filer.
/// </summary>
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

    public BytePool.RentedReadOnlyMemory WriteData { get; private set; }

    public BytePool.RentedMemory ReadData { get; internal set; }

    public object? OutputObject { get; internal set; }

    public FilerWork()
    {
    }

    public void Initialize(string path, long offset, BytePool.RentedReadOnlyMemory dataToBeShared, bool truncate)
    {// Write
        this.Reset(WorkType.Write, path);
        this.Offset = offset;
        this.Truncate = truncate;
        this.WriteData = dataToBeShared.IncrementAndShare();
    }

    public void Initialize(string path, long offset, int length)
    {// Read
        this.Reset(WorkType.Read, path);
        this.Offset = offset;
        this.Length = length;
    }

    public void Initialize(WorkType workType, string path)
    {// Delete/List
        this.Reset(workType, path);
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

    private void Reset(WorkType workType, string path)
    {// Pooled jobs are reused, so every field is reset (e.g. a stale Success must not be reported for an aborted job).
        this.Type = workType;
        this.Result = CrystalResult.NotStarted;
        this.Path = path;
        this.Offset = 0;
        this.Length = 0;
        this.Truncate = false;
        this.WriteData = default;
        this.ReadData = default;
        this.OutputObject = null;
    }
}
