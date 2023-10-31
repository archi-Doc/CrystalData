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
        set => this.position = value;
    }

    public override void Flush() => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (this.position < this.memory.Length)
        {
            var lengthToRead = this.memory.Length - (int)this.position;
            var bufferRemaining = buffer.Length - offset;
            lengthToRead = (lengthToRead < count) ? lengthToRead : count;
            lengthToRead = (lengthToRead < bufferRemaining) ? lengthToRead : bufferRemaining;

            if (lengthToRead > 0)
            {
                this.memory.Span.Slice((int)this.position, lengthToRead).CopyTo(buffer.AsSpan(offset));
                this.position += lengthToRead;
                return lengthToRead;
            }
            else
            {
                return 0;
            }
        }
        else
        {
            return 0;
        }
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
        if (origin == SeekOrigin.Begin)
        {
            this.position = offset;
        }
        else if (origin == SeekOrigin.Current)
        {
            this.position += offset;
        }
        else if (origin == SeekOrigin.End)
        {
            this.position = this.memory.Length + offset;
        }

        return this.position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private ReadOnlyMemory<byte> memory;
    private long position;
}
