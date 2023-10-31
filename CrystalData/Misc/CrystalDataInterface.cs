// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Tinyhand.IO;

namespace CrystalData;

/*[TinyhandObject]
public partial class CrystalDataInterface : ITinyhandSerialize<CrystalDataInterface>, ITinyhandReconstruct<CrystalDataInterface>, IStructualObject
{
    public CrystalDataInterface()
    {
    }

    static void ITinyhandSerialize<CrystalDataInterface>.Serialize(ref TinyhandWriter writer, scoped ref CrystalDataInterface? value, TinyhandSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteSpan(value.data);
    }

    static void ITinyhandSerialize<CrystalDataInterface>.Deserialize(ref TinyhandReader reader, scoped ref CrystalDataInterface? value, TinyhandSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return;
        }

        value ??= new();
        value.data = reader.ReadRaw(reader.Remaining).ToArray(); // tempcode
    }

    static void ITinyhandReconstruct<CrystalDataInterface>.Reconstruct([NotNull] scoped ref CrystalDataInterface? value, TinyhandSerializerOptions options)
    {
        value ??= new();
    }

    private byte[] data = Array.Empty<byte>();

    public byte[] Data
    {
        get => this.data;
        set
        {
            this.data = value;
            this.TryAddToSaveQueue();
        }
    }

    public bool TryAddToSaveQueue()
        => ((IStructualObject)this).StructualRoot?.TryAddToSaveQueue() == true;

    [IgnoreMember]
    IStructualRoot? IStructualObject.StructualRoot { get; set; }

    [IgnoreMember]
    IStructualObject? IStructualObject.StructualParent { get; set; }

    [IgnoreMember]
    int IStructualObject.StructualKey { get; set; } = -1;

    void IStructualObject.SetParent(IStructualObject? parent, int key)
    {
        ((IStructualObject)this).SetParentActual(parent, key);
    }

    bool IStructualObject.ReadRecord(ref TinyhandReader reader)
    {
        return false;
    }
}
*/
