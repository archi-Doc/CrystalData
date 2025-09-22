// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CrystalData.Filer;
using Tinyhand.IO;

namespace CrystalData.Journal;

public partial class SimpleJournal : IJournal
{
    public const string CompleteSuffix = ".complete";
    public const string IncompleteSuffix = ".incomplete";
    public const int RecordBufferLength = 1024 * 1024 * 1; // 1MB
    private const int MergeThresholdNumber = 100;

    public SimpleJournal(CrystalControl crystalControl, SimpleJournalConfiguration configuration, ILogger<SimpleJournal> logger)
    {
        this.crystalControl = crystalControl;
        this.SimpleJournalConfiguration = configuration;
        this.MainConfiguration = this.SimpleJournalConfiguration.DirectoryConfiguration;
        this.BackupConfiguration = this.SimpleJournalConfiguration.BackupDirectoryConfiguration;
        this.logger = logger;
    }

    #region PropertyAndField

    public SimpleJournalConfiguration SimpleJournalConfiguration { get; }

    public DirectoryConfiguration MainConfiguration { get; private set; }

    public DirectoryConfiguration? BackupConfiguration { get; private set; }

    private CrystalControl crystalControl;
    private bool prepared;
    private IFiler? rawFiler;
    private IFiler? backupFiler;
    private SimpleJournalTask? task;

    // Record buffer: lockRecordBuffer
    private Lock lockRecordBuffer = new(); // lockBooks > lockRecordBuffer
    private byte[] recordBuffer = new byte[RecordBufferLength];
    private ulong recordBufferPosition = 1; // JournalPosition
    private int recordBufferLength = 0;

    private int recordBufferRemaining => RecordBufferLength - this.recordBufferLength;

    // Books: lockBooks
    private Lock lockBooks = new();
    private Book.GoshujinClass books = new();
    private int memoryUsage;
    private ulong incompleteSize;

    internal ILogger logger { get; private set; }

    #endregion

    public async Task<CrystalResult> Prepare(PrepareParam param)
    {
        if (this.prepared)
        {
            return CrystalResult.Success;
        }

        if (this.rawFiler == null)
        {
            (this.rawFiler, this.MainConfiguration) = this.crystalControl.ResolveFiler(this.MainConfiguration);
            var result = await this.rawFiler.PrepareAndCheck(param, this.MainConfiguration).ConfigureAwait(false);
            if (result != CrystalResult.Success)
            {
                return result;
            }
        }

        if (this.BackupConfiguration is not null &&
            this.backupFiler == null)
        {
            (this.backupFiler, this.BackupConfiguration) = this.crystalControl.ResolveFiler(this.BackupConfiguration);
            var result = await this.backupFiler.PrepareAndCheck(param, this.BackupConfiguration).ConfigureAwait(false);
            if (result != CrystalResult.Success)
            {
                return result;
            }
        }

        // List main books
        var mainList = await this.ListBooks(this.rawFiler, this.MainConfiguration).ConfigureAwait(false);
        this.books = mainList.Books;
        this.recordBufferPosition = mainList.Position;

        // List backup books
        if (this.backupFiler is not null && this.BackupConfiguration is not null)
        {
            var backupList = await this.ListBooks(this.backupFiler, this.BackupConfiguration).ConfigureAwait(false);
        }

        if (this.task is null)
        {
            this.task = new(this);
            this.task.Start();
        }

        this.logger.TryGet()?.Log($"Prepared: {this.books.PositionChain.First?.Position} - {this.books.PositionChain.Last?.NextPosition} ({this.books.PositionChain.Count})");

        await this.Merge(false).ConfigureAwait(false);

        this.prepared = true;
        return CrystalResult.Success;
    }

    int IJournal.MaxRecordLength => this.SimpleJournalConfiguration.MaxRecordLength;

    void IJournal.GetWriter(JournalType recordType, out TinyhandWriter writer)
    {
        writer = TinyhandWriter.CreateFromBytePool();
        writer.Advance(3); // Size(0-16MB): byte[3]
        writer.WriteUnsafe(Unsafe.As<JournalType, byte>(ref recordType)); // JournalRecordType: byte
    }

    ulong IJournal.Add(ref TinyhandWriter writer)
    {
        var rentMemory = writer.FlushAndGetRentMemory();
        writer.Dispose();
        try
        {
            var memory = rentMemory.Memory;
            if (memory.Length > this.SimpleJournalConfiguration.MaxRecordLength)
            {
                // throw new InvalidOperationException($"The maximum length per record is {this.SimpleJournalConfiguration.MaxRecordLength} bytes.");
                this.logger.TryGet(LogLevel.Error)?.Log($"The maximum length per record is {this.SimpleJournalConfiguration.MaxRecordLength} bytes.");
                return ((IJournal)this).GetCurrentPosition();
            }

            // Size (0-16MB)
            var span = memory.Span;
            var length = memory.Length - 4;
            span[2] = (byte)length;
            span[1] = (byte)(length >> 8);
            span[0] = (byte)(length >> 16);

            using (this.lockRecordBuffer.EnterScope())
            {
                if (this.recordBufferRemaining < span.Length)
                {
                    this.FlushRecordBufferInternal();
                }

                span.CopyTo(this.recordBuffer.AsSpan(this.recordBufferLength));
                this.recordBufferLength += span.Length;
                return this.recordBufferPosition + (ulong)this.recordBufferLength;
            }
        }
        finally
        {
            rentMemory.Return();
        }
    }

    Task<CrystalResult> IPersistable.StoreData(StoreMode storeMode, CancellationToken cancellationToken)
        => this.StoreJournalAsync(true, storeMode, cancellationToken);

    Type IPersistable.DataType => typeof(SimpleJournal);

    async Task IJournal.Terminate()
    {
        if (this.task is { } task)
        {// Wait for the task to complete; the journal is written upon termination.
            task.Terminate();
            await task.WaitForTerminationAsync(-1).ConfigureAwait(false);
        }

        using (this.lockBooks.EnterScope())
        {
            var array = this.books.ToArray();
            foreach (var x in array)
            {
                x.Goshujin = null;
            }
        }

        this.logger.TryGet()?.Log($"Terminated - {this.memoryUsage}");
    }

    ulong IJournal.GetStartingPosition()
    {
        using (this.lockBooks.EnterScope())
        {
            if (this.books.PositionChain.First is { } firstBook)
            {
                return firstBook.Position;
            }
            else
            {
                return 0;
            }
        }
    }

    ulong IJournal.GetCurrentPosition()
    {
        using (this.lockRecordBuffer.EnterScope())
        {
            return this.recordBufferPosition + (ulong)this.recordBufferLength;
        }
    }

    void IJournal.ResetJournal(ulong position)
    {
        using (this.lockBooks.EnterScope())
        {
            var array = this.books.ToArray();
            foreach (var x in array)
            {
                x.DeleteInternal();
            }

            this.books.Clear();

            using (this.lockRecordBuffer.EnterScope())
            {
                this.recordBufferPosition = position;
                this.recordBufferLength = 0;
            }
        }
    }

    public async Task<(ulong NextPosition, BytePool.RentMemory Data)> ReadJournalAsync(ulong position)
    {
        ulong length, nextPosition;
        using (this.lockBooks.EnterScope())
        {
            var startBook = this.books.PositionChain.GetUpperBound(position);
            if (startBook == null || startBook.NextPosition <= position)
            {
                return (0, default);
            }

            var endBook = this.books.PositionChain.GetUpperBound(position + (ulong)this.SimpleJournalConfiguration.CompleteBookLength);
            if (endBook == null)
            {
                return (0, default);
            }

            length = endBook.NextPosition - position; // > 0
            nextPosition = endBook.NextPosition;
        }

        var memoryOwner = BytePool.Default.Rent((int)length).AsMemory(0, (int)length);
        var success = await this.ReadJournalAsync(position, nextPosition, memoryOwner.Memory).ConfigureAwait(false);
        if (!success)
        {
            return (0, default);
        }

        return (nextPosition, memoryOwner);
    }

    public async Task<bool> ReadJournalAsync(ulong start, ulong end, Memory<byte> data)
    {// [start, end) = [start, end -1]
        var length = (int)(end - start);
        if (data.Length < length)
        {
            return false;
        }

        var retry = 0;
        List<(ulong Position, string Path)> loadList = new();

Load:
        if (retry++ >= 2 || this.rawFiler == null)
        {
            return false;
        }

        foreach (var x in loadList)
        {
            var result = await this.rawFiler.ReadAsync(x.Path, 0, -1).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return false;
            }

            try
            {
                using (this.lockBooks.EnterScope())
                {
                    var book = this.books.PositionChain.FindFirst(x.Position);
                    if (book is not null)
                    {
                        book.TrySetBuffer(result.Data);
                    }
                }
            }
            finally
            {
                result.Return();
            }
        }

        using (this.lockBooks.EnterScope())
        {
            var startBook = this.books.PositionChain.GetUpperBound(start);
            var endBook = this.books.PositionChain.GetUpperBound(end - 1);
            if (startBook is null || endBook is null)
            {
                return false;
            }
            else if (endBook.NextPosition < end)
            {
                return false;
            }

            // range.Lower.Position <= start, range.Upper.Position < end

            // Check
            loadList.Clear();
            for (var book = startBook; book != null; book = book.PositionLink.Next)
            {
                if (!book.IsInMemory)
                {// Load (start, path)
                    if (book.Path is not null)
                    {
                        loadList.Add((book.Position, book.Path));
                    }
                }

                if (book == endBook)
                {
                    break;
                }
            }

            // Load
            if (loadList.Count > 0)
            {
                goto Load;
            }

            // Read
            var dataPosition = 0;
            for (var book = startBook; book != null; book = book.PositionLink.Next)
            {
                if (!book.TryReadBufferInternal(start, data.Span.Slice(dataPosition), out var readLength))
                {// Fatal
                    return false;
                }

                dataPosition += readLength;
                start += (ulong)readLength;

                if (book == endBook)
                {// Complete
                    return true;
                }
            }

            return false;
        }
    }

    Task<bool> IPersistable.TestJournal()
        => Task.FromResult(true);

    internal async Task<CrystalResult> StoreJournalAsync(bool mergeBooks, StoreMode storeMode, CancellationToken cancellationToken)
    {
        using (this.lockBooks.EnterScope())
        {
            using (this.lockRecordBuffer.EnterScope())
            {// Flush record buffer
                this.FlushRecordBufferInternal();
            }

            // Save all books
            Book? book = this.books.PositionChain.Last;
            Book? next = null;
            while (book != null && !book.IsSaved)
            {
                next = book;
                book = book.PositionLink.Previous;
            }

            book = next;
            while (book != null)
            {
                book.SaveInternal();
                book = book.PositionLink.Next;
            }

            // Limit memory usage
            while (this.memoryUsage > this.SimpleJournalConfiguration.MaxMemoryCapacity)
            {
                if (this.books.InMemoryChain.First is { } b)
                {
                    b.Goshujin = null;
                }
            }

            if (!mergeBooks)
            {
                return CrystalResult.Success;
            }

            if (this.books.IncompleteChain.Count >= MergeThresholdNumber ||
            this.incompleteSize >= (ulong)this.SimpleJournalConfiguration.CompleteBookLength)
            {
                mergeBooks = true;
            }
            else
            {
                mergeBooks = false;
            }
        }

        if (mergeBooks)
        { // Merge books
            await this.Merge(false).ConfigureAwait(false);
        }

        return CrystalResult.Success;
    }

    internal async Task Merge(bool forceMerge)
    {
        var book = this.books.IncompleteChain.Last;
        var incompleteCount = 0;
        var incompleteLength = 0;
        var lastLength = 0;
        ulong start, end;

        using (this.lockBooks.EnterScope())
        {
            while (book != null)
            {
                incompleteCount++;
                incompleteLength += book.Length;
                if (incompleteLength <= this.SimpleJournalConfiguration.CompleteBookLength)
                {
                    lastLength = incompleteLength;
                }

                book = book.IncompleteLink.Previous;
            }

            Debug.Assert(incompleteCount == this.books.IncompleteChain.Count);

            if (!forceMerge)
            {
                if (incompleteCount < MergeThresholdNumber ||
                    incompleteLength < this.SimpleJournalConfiguration.CompleteBookLength)
                {
                    return;
                }
            }

            start = this.books.IncompleteChain.First!.Position;
            end = start + (ulong)lastLength;
        }

        if (incompleteCount < 2)
        {
            return;
        }

        var owner = BytePool.Default.Rent(lastLength);
        if (!await this.ReadJournalAsync(start, end, owner.AsMemory(0, lastLength).Memory).ConfigureAwait(false))
        {
            owner.Return();
            return;
        }

        if (await Book.MergeBooks(this, start, end, owner.AsReadOnly(0, lastLength)).ConfigureAwait(false))
        {// Success
            this.logger.TryGet()?.Log($"Merged: {start} - {end}");
        }
    }

    private void FlushRecordBufferInternal()
    {// using (this.lockRecordBuffer.EnterScope())
        if (this.recordBufferLength == 0)
        {// Empty
            return;
        }

        Book.AppendNewBook(this, this.recordBufferPosition, this.recordBuffer, this.recordBufferLength);

        this.recordBufferPosition += (ulong)this.recordBufferLength;
        this.recordBufferLength = 0;
    }

    private async Task<(Book.GoshujinClass Books, ulong Position)> ListBooks(IFiler rawFiler, DirectoryConfiguration directoryConfiguration)
    {
        var list = await rawFiler.ListAsync(directoryConfiguration.Path).ConfigureAwait(false);
        if (list == null)
        {
            return (new Book.GoshujinClass(), 1);
        }

        Book.GoshujinClass books = new();
        foreach (var x in list)
        {
            _ = Book.TryAdd(this, books, x);
        }

        foreach (var x in books)
        {
            if (x.IsIncomplete)
            {
                books.IncompleteChain.AddLast(x);
            }
        }

        var position = this.CheckBooksInternal(books);
        return (books, position);
    }

    private ulong CheckBooksInternal(Book.GoshujinClass books)
    {
        ulong position = 1; // Initial position
        ulong previousPosition = 0;
        Book? previous = null;
        Book? toDelete = null;

        foreach (var book in books)
        {
            if (previous != null && book.Position != previousPosition)
            {
                toDelete = previous;
            }

            previousPosition = book.NextPosition;
            previous = book;
        }

        if (toDelete == null)
        {// Ok
            if (books.PositionChain.Last is not null)
            {
                return books.PositionChain.Last.NextPosition;
            }
            else
            {// Initial position
                return position;
            }
        }
        else
        {
            var nextBook = toDelete.PositionLink.Next;
            if (nextBook is not null)
            {
                position = nextBook.NextPosition;
            }
            else
            {
                position = toDelete.NextPosition;
            }
        }

        while (true)
        {// Delete books that have lost journal consistency.
            var first = books.PositionChain.First;
            if (first == null)
            {
                return position;
            }

            first.DeleteInternal();
            if (first == toDelete)
            {
                return position;
            }
        }
    }
}
