// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Filer;

public sealed class ReadOnlyMemoryStream : Stream
{
    public ReadOnlyMemoryStream(ReadOnlyMemory<byte> memory)
    {
        this.memory = memory;
        this.position = 0;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => this.memory.Length;

    public override long Position
    {
        get => this.position;
        set
        {
            if ((ulong)value > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            this.position = value;
        }
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return this.Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        if (this.position < this.memory.Length)
        {
            var lengthToRead = this.memory.Length - (int)this.position;
            lengthToRead = (lengthToRead < buffer.Length) ? lengthToRead : buffer.Length;

            this.memory.Span.Slice((int)this.position, lengthToRead).CopyTo(buffer);
            this.position += lengthToRead;
            return lengthToRead;
        }
        else
        {
            return 0;
        }
    }

    public override int ReadByte()
    {
        if (this.position < this.memory.Length)
        {
            return this.memory.Span[(int)this.position++];
        }
        else
        {
            return -1;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPosition;
        try
        {
            newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(this.position + offset),
                SeekOrigin.End => checked(this.memory.Length + offset),
                _ => throw new ArgumentException("Invalid seek origin.", nameof(origin)),
            };
        }
        catch (OverflowException ex)
        {
            throw new IOException("An attempt was made to move the position outside the valid stream range.", ex);
        }

        this.Position = newPosition;
        return newPosition;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private ReadOnlyMemory<byte> memory;
    private long position;
}
