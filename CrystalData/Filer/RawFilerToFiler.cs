// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Filer;

internal class RawFilerToFiler : ISingleFiler
{
    internal RawFilerToFiler(Crystalizer crystalizer, IFiler rawFiler, string path)
    {
        this.Crystalizer = crystalizer;
        this.RawFiler = rawFiler;
        this.Path = path;
        this.timeout = crystalizer.Options.FilerTimeout;
    }

    public Crystalizer Crystalizer { get; }

    public IFiler RawFiler { get; }

    public string Path { get; }

    bool ISingleFiler.SupportPartialWrite => this.RawFiler.SupportPartialWrite;

    void ISingleFiler.SetTimeout(TimeSpan timeout)
    {
        this.timeout = timeout;
    }

    CrystalResult ISingleFiler.DeleteAndForget()
        => this.RawFiler.DeleteAndForget(this.Path);

    Task<CrystalResult> ISingleFiler.DeleteAsync()
        => this.RawFiler.DeleteAsync(this.Path, this.timeout);

    Task<CrystalResult> ISingleFiler.PrepareAndCheck(PrepareParam param, PathConfiguration configuration)
         => this.RawFiler.PrepareAndCheck(param, configuration);

    Task<CrystalMemoryOwnerResult> ISingleFiler.ReadAsync(long offset, int length)
        => this.RawFiler.ReadAsync(this.Path, offset, length, this.timeout);

    CrystalResult ISingleFiler.WriteAndForget(long offset, BytePool.RentReadOnlyMemory dataToBeShared, bool truncate)
        => this.RawFiler.WriteAndForget(this.Path, offset, dataToBeShared, truncate);

    Task<CrystalResult> ISingleFiler.WriteAsync(long offset, BytePool.RentReadOnlyMemory dataToBeShared, bool truncate)
        => this.RawFiler.WriteAsync(this.Path, offset, dataToBeShared, this.timeout, truncate);

    ISingleFiler ISingleFiler.CloneWithExtension(string extension)
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
