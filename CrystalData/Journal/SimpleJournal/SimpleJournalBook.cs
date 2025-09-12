// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using CrystalData.Filer;

namespace CrystalData.Journal;

public partial class SimpleJournal
{
    private enum BookType
    {
        Incomplete,
        Complete,
    }

    [ValueLinkObject]
    private partial class Book
    {
        [Link(Type = ChainType.LinkedList, Name = "InMemory")]
        [Link(Type = ChainType.LinkedList, Name = "Incomplete")]
        private Book(SimpleJournal simpleJournal)
        {
            this.simpleJournal = simpleJournal;
        }

        #region PropertyAndField

        internal ulong Position => this.position;

        internal ulong NextPosition => this.position + (ulong)this.length;

        internal int Length => this.length;

        internal string? Path => this.path;

        // internal string? BackupPath => this.backupPath;

        internal bool IsSaved => this.path != null;

        internal bool IsInMemory => this.memoryOwner.Memory.Length > 0;

        internal bool IsIncomplete => this.bookType == BookType.Incomplete;

        private SimpleJournal simpleJournal;

        [Link(Primary = true, Type = ChainType.Ordered, AddValue = false)]
        private ulong position;

        private int length;
        private BookType bookType;
        private string? path;
        private string? backupPath;
        private ulong hash;
        private BytePool.RentReadOnlyMemory memoryOwner;

        #endregion

        public static Book? TryAdd(SimpleJournal simpleJournal, Book.GoshujinClass books, PathInformation pathInformation)
        {
            if (pathInformation.Length == 0)
            {// Empty
                return null;
            }

            BookType bookType;

            // BookTitle.complete or BookTitle.incomplete
            var fileName = System.IO.Path.GetFileName(pathInformation.Path);
            if (fileName.EndsWith(CompleteSuffix))
            {
                bookType = BookType.Complete;
                fileName = fileName.Substring(0, fileName.Length - CompleteSuffix.Length);
            }
            else if (fileName.EndsWith(IncompleteSuffix))
            {
                bookType = BookType.Incomplete;
                fileName = fileName.Substring(0, fileName.Length - IncompleteSuffix.Length);
            }
            else
            {
                return null;
            }

            if (!BookTitle.TryParse(fileName, out var bookTitle))
            {
                return null;
            }

            var book = new Book(simpleJournal);
            book.position = bookTitle.JournalPosition;
            book.length = (int)pathInformation.Length;
            book.path = pathInformation.Path;
            book.bookType = BookType.Complete;
            book.hash = bookTitle.Hash;

            book.Goshujin = books;

            // Delay setting the book type for sorting later.
            book.bookType = bookType;

            return book;
        }

        public static Book? AppendNewBook(SimpleJournal simpleJournal, ulong position, byte[] data, int dataLength)
        {
            if (data.Length == 0)
            {
                return default;
            }

            var book = new Book(simpleJournal);
            book.position = position;
            book.length = dataLength;
            book.bookType = BookType.Incomplete;

            var owner = BytePool.Default.Rent(dataLength);
            data.AsSpan(0, dataLength).CopyTo(owner.AsSpan());
            book.memoryOwner = owner.AsReadOnly(0, dataLength);
            book.hash = FarmHash.Hash64(book.memoryOwner.Span);

            using (simpleJournal.lockBooks.EnterScope())
            {
                book.Goshujin = simpleJournal.books;
            }

            return book;
        }

        public static async Task<bool> MergeBooks(SimpleJournal simpleJournal, ulong start, ulong end, BytePool.RentReadOnlyMemory toBeMoved)
        {
            var book = new Book(simpleJournal);
            book.position = start;
            book.length = (int)(end - start);
            if (book.length < (simpleJournal.SimpleJournalConfiguration.CompleteBookLength / 2))
            {// Length < (CompleteBookLength/2) -> Incomplete
                book.bookType = BookType.Incomplete;
            }
            else
            {// Length >= (CompleteBookLength/2) -> Complete
                book.bookType = BookType.Complete;
            }

            book.memoryOwner = toBeMoved;
            book.hash = FarmHash.Hash64(toBeMoved.Memory.Span);

            // Save the merged book first
            if (await book.SaveAsync().ConfigureAwait(false) == false)
            {
                return false;
            }

            using (simpleJournal.lockBooks.EnterScope())
            {
                var range = simpleJournal.books.PositionChain.GetRange(start, end - 1);
                if (range.Lower == null || range.Upper == null)
                {
                    return false;
                }
                else if (range.Lower.position != start || range.Upper.NextPosition != end)
                {
                    return false;
                }

                // Delete books
                var b = range.Lower;
                while (b != null)
                {
                    var b2 = b.PositionLink.Next;
                    b.DeleteInternal();
                    if (b == range.Upper)
                    {
                        break;
                    }

                    b = b2;
                }

                // Add the merged book
                book.Goshujin = simpleJournal.books;
            }

            return true; // Success
        }

        public void SaveInternal()
        {// using (core.simpleJournal.lockBooks.EnterScope())
            if (this.IsSaved)
            {
                return;
            }
            else if (!this.IsInMemory)
            {
                return;
            }
            else if (this.simpleJournal.rawFiler == null)
            {
                return;
            }

            // Write (IsSaved -> true)
            this.path = StorageHelper.CombineWithSlash(this.simpleJournal.MainConfiguration.Path, this.GetFileName());
            this.simpleJournal.rawFiler.WriteAndForget(this.path, 0, this.memoryOwner);

            if (this.simpleJournal.BackupConfiguration is not null &&
                this.simpleJournal.backupFiler is not null)
            {
                this.backupPath ??= StorageHelper.CombineWithSlash(this.simpleJournal.BackupConfiguration.Path, this.GetFileName());
                this.simpleJournal.backupFiler.WriteAndForget(this.backupPath, 0, this.memoryOwner);
            }
        }

        public bool TryReadBufferInternal(ulong position, Span<byte> destination, out int readLength)
        {
            readLength = 0;
            if (position < this.position || position >= this.NextPosition)
            {
                return false;
            }

            var length = (int)(this.NextPosition - position);
            if (destination.Length < length)
            {
                length = destination.Length;
            }

            if (!this.IsInMemory)
            {
                return false;
            }

            this.memoryOwner.Memory.Span.Slice((int)(position - this.position), length).CopyTo(destination);
            readLength = length;
            return true;
        }

        public async Task<bool> SaveAsync()
        {
            if (this.IsSaved)
            {
                return false;
            }
            else if (!this.IsInMemory)
            {
                return false;
            }
            else if (this.simpleJournal.rawFiler == null)
            {
                return false;
            }

            // Write (IsSaved -> true)
            this.path = StorageHelper.CombineWithSlash(this.simpleJournal.SimpleJournalConfiguration.DirectoryConfiguration.Path, this.GetFileName());
            var result = await this.simpleJournal.rawFiler.WriteAsync(this.path, 0, this.memoryOwner).ConfigureAwait(false);

            if (this.simpleJournal.BackupConfiguration is not null &&
                this.simpleJournal.backupFiler is not null)
            {
                this.backupPath ??= StorageHelper.CombineWithSlash(this.simpleJournal.BackupConfiguration.Path, this.GetFileName());
                _ = this.simpleJournal.backupFiler.WriteAsync(this.backupPath, 0, this.memoryOwner);
            }

            return result.IsSuccess();
        }

        public void DeleteInternal()
        {
            if (this.path != null && this.simpleJournal.rawFiler is { } rawFiler)
            {
                rawFiler.DeleteAndForget(this.path);
            }

            if (this.simpleJournal.backupFiler is not null &&
                this.simpleJournal.BackupConfiguration is not null)
            {
                this.backupPath ??= StorageHelper.CombineWithSlash(this.simpleJournal.BackupConfiguration.Path, this.GetFileName());
                this.simpleJournal.backupFiler.DeleteAndForget(this.backupPath);
            }

            this.Goshujin = null;
        }

        public bool TrySetBuffer(BytePool.RentReadOnlyMemory data)
        {
            if (this.IsInMemory)
            {
                return false;
            }
            else if (this.length != data.Memory.Length)
            {
                return false;
            }

            this.memoryOwner = data.IncrementAndShare();
            this.simpleJournal.books.InMemoryChain.AddLast(this);
            this.InMemoryLinkAdded();

            return true;
        }

        public override string ToString()
            => $"Book [{this.Position}, {this.NextPosition})";

        protected bool IncompleteLinkPredicate()
            => this.IsIncomplete;

        protected void IncompleteLinkAdded()
        {
            this.simpleJournal.incompleteSize += (ulong)this.length;
        }

        protected void IncompleteLinkRemoved()
        {
            this.simpleJournal.incompleteSize -= (ulong)this.length;
        }

        protected bool InMemoryLinkPredicate()
            => this.IsInMemory;

        protected void InMemoryLinkAdded()
        {
            this.simpleJournal.memoryUsage += this.memoryOwner.Memory.Length;
        }

        protected void InMemoryLinkRemoved()
        {
            this.simpleJournal.memoryUsage -= this.memoryOwner.Memory.Length;
            this.memoryOwner = this.memoryOwner.Return();
        }

        private string GetFileName()
        {
            var bookTitle = new BookTitle(this.position, this.hash);
            return bookTitle.ToBase32() + (this.bookType == BookType.Complete ? CompleteSuffix : IncompleteSuffix);
        }
    }
}
