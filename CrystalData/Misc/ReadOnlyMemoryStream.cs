// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Filer;

/// <summary>
/// Exposes a <see cref="ReadOnlyMemory{T}"/> of bytes as a readable, seekable stream.
/// </summary>
/// <remarks>The caller owns the backing memory and must keep it valid until the stream is disposed.</remarks>
public sealed class ReadOnlyMemoryStream : Stream
{
    public ReadOnlyMemoryStream(ReadOnlyMemory<byte> memory)
    {
        this.memory = memory;
        this.position = 0;
    }

    public override bool CanRead => !this.disposed;

    public override bool CanSeek => !this.disposed;

    public override bool CanWrite => false;

    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            return this.memory.Length;
        }
    }

    public override long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            return this.position;
        }

        set
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if ((ulong)value > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            this.position = value;
        }
    }

    public override void Flush()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return this.Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
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

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {// Completes synchronously (the base implementation queues the read to the thread pool).
        ValidateBufferArguments(buffer, offset, count);
        return cancellationToken.IsCancellationRequested ?
            Task.FromCanceled<int>(cancellationToken) :
            Task.FromResult(this.Read(buffer.AsSpan(offset, count)));
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => cancellationToken.IsCancellationRequested ?
            ValueTask.FromCanceled<int>(cancellationToken) :
            new(this.Read(buffer.Span));

    public override int ReadByte()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
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
        ObjectDisposedException.ThrowIf(this.disposed, this);
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

    protected override void Dispose(bool disposing)
    {
        this.disposed = true;
        this.memory = default;
        base.Dispose(disposing);
    }

    private ReadOnlyMemory<byte> memory;
    private long position;
    private bool disposed;
}
