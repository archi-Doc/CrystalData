// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Filer;

internal class RawFilerToFiler : IFiler
{
    internal RawFilerToFiler(Crystalizer crystalizer, IRawFiler rawFiler, string path)
    {
        this.Crystalizer = crystalizer;
        this.RawFiler = rawFiler;
        this.Path = path;
        this.timeout = crystalizer.FilerTimeout;
    }

    public Crystalizer Crystalizer { get; }

    public IRawFiler RawFiler { get; }

    public string Path { get; }

    bool IFiler.SupportPartialWrite => this.RawFiler.SupportPartialWrite;

    void IFiler.SetTimeout(TimeSpan timeout)
    {
        this.timeout = timeout;
    }

    CrystalResult IFiler.DeleteAndForget()
        => this.RawFiler.DeleteAndForget(this.Path);

    Task<CrystalResult> IFiler.DeleteAsync()
        => this.RawFiler.DeleteAsync(this.Path, this.timeout);

    Task<CrystalResult> IFiler.PrepareAndCheck(PrepareParam param, PathConfiguration configuration)
         => this.RawFiler.PrepareAndCheck(param, configuration);

    Task<CrystalMemoryOwnerResult> IFiler.ReadAsync(long offset, int length)
        => this.RawFiler.ReadAsync(this.Path, offset, length, this.timeout);

    CrystalResult IFiler.WriteAndForget(long offset, BytePool.RentReadOnlyMemory dataToBeShared, bool truncate)
        => this.RawFiler.WriteAndForget(this.Path, offset, dataToBeShared, truncate);

    Task<CrystalResult> IFiler.WriteAsync(long offset, BytePool.RentReadOnlyMemory dataToBeShared, bool truncate)
        => this.RawFiler.WriteAsync(this.Path, offset, dataToBeShared, this.timeout, truncate);

    IFiler IFiler.CloneWithExtension(string extension)
    {
        string path;
        try
        {
            path = System.IO.Path.ChangeExtension(this.Path, extension);
        }
        catch
        {
            path = $"{this.Path}.{extension}";
        }

        return new RawFilerToFiler(this.Crystalizer, this.RawFiler, path);
    }

    public override string ToString()
        => $"RawFilerToFile({this.RawFiler.ToString()}) Path:{this.Path}";

    private TimeSpan timeout;
}
