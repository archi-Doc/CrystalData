// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Collections.Concurrent;
using CrystalData.Results;

namespace CrystalData.Filer;

public class LocalFiler : FilerBase, IRawFiler
{
    public LocalFiler()
        : base(Process)
    {
    }

    public static AddStorageResult Check(Crystalizer crystalizer, string directory)
    {
        var result = CheckPath(crystalizer, directory);
        if (!result.Success)
        {
            return AddStorageResult.WriteError;
        }

        return AddStorageResult.Success;
    }

    public static async Task Process(TaskWorker<FilerWork> w, FilerWork work)
    {
        var worker = (LocalFiler)w;
        var tryCount = 0;

        var filePath = Crystalizer.GetRootedFile(worker.Crystalizer, work.Path);
        work.Result = CrystalResult.Started;
        // Console.WriteLine($"{work.ToString()} -> {filePath}");
        if (work.Type == FilerWork.WorkType.Write)
        {// Write
            try
            {
TryWrite:
                tryCount++;
                if (tryCount > 2)
                {
                    work.Result = CrystalResult.FileOperationError;
                    return;
                }

                try
                {
                    using (var handle = File.OpenHandle(filePath, mode: FileMode.OpenOrCreate, access: FileAccess.Write))
                    {
                        await RandomAccess.WriteAsync(handle, work.WriteData.Memory, work.Offset, worker.CancellationToken).ConfigureAwait(false);
                        worker.logger?.TryGet(LogLevel.Debug)?.Log($"Written[{work.WriteData.Memory.Length}] {work.Path}");

                        if (work.Truncate)
                        {
                            try
                            {
                                var newSize = work.Offset + work.WriteData.Memory.Length;
                                if (RandomAccess.GetLength(handle) > newSize)
                                {
                                    RandomAccess.SetLength(handle, newSize);
                                }
                            }
                            catch
                            {
                            }
                        }

                        work.Result = CrystalResult.Success;
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    if (Path.GetDirectoryName(filePath) is string directoryPath)
                    {// Create directory
                        try
                        {
                            Directory.CreateDirectory(directoryPath);
                        }
                        catch
                        {
                            work.Result = CrystalResult.FileOperationError;
                            return;
                        }

                        worker.logger?.TryGet(LogLevel.Debug)?.Log($"Directory created: {directoryPath}");
                    }
                    else
                    {
                        work.Result = CrystalResult.FileOperationError;
                        return;
                    }

                    goto TryWrite;
                }
                catch (OperationCanceledException)
                {
                    work.Result = CrystalResult.Aborted;
                    return;
                }
                catch
                {
                    worker.logger?.TryGet(LogLevel.Warning)?.Log($"Retry {work.Path}");
                    goto TryWrite;
                }
            }
            finally
            {
                work.WriteData.Return();
            }
        }
        else if (work.Type == FilerWork.WorkType.Read)
        {// Read
            try
            {
                var offset = work.Offset;
                var lengthToRead = work.Length;
                if (lengthToRead < 0)
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        lengthToRead = (int)fileInfo.Length;
                        offset = 0;
                    }
                    catch
                    {
                        work.Result = CrystalResult.FileOperationError;
                        return;
                    }
                }

                using (var handle = File.OpenHandle(filePath, mode: FileMode.Open, access: FileAccess.Read))
                {
                    var memoryOwner = BytePool.Default.Rent(lengthToRead).AsMemory(0, lengthToRead);
                    var read = await RandomAccess.ReadAsync(handle, memoryOwner.Memory, offset, worker.CancellationToken).ConfigureAwait(false);
                    // Console.WriteLine($"Read {filePath} {read.ToString()}");
                    if (read != lengthToRead)
                    {
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch
                        {
                        }

                        worker.logger?.TryGet(LogLevel.Error)?.Log($"Read error and deleted: {work.Path}");
                        work.Result = CrystalResult.FileOperationError;
                        return;
                    }

                    work.Result = CrystalResult.Success;
                    work.ReadData = memoryOwner;
                    worker.logger?.TryGet(LogLevel.Debug)?.Log($"Read[{memoryOwner.Memory.Length}] {work.Path}");
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                work.Result = CrystalResult.Aborted;
                return;
            }
            catch
            {
                work.Result = CrystalResult.FileOperationError;
                worker.logger?.TryGet(LogLevel.Error)?.Log($"Read exception {work.Path}");
            }
            finally
            {
            }
        }
        else if (work.Type == FilerWork.WorkType.Delete)
        {// Delete
            try
            {
                File.Delete(filePath);
                worker.logger?.TryGet(LogLevel.Debug)?.Log($"Deleted: {work.Path}");
                work.Result = CrystalResult.Success;
            }
            catch
            {
                work.Result = CrystalResult.FileOperationError;
            }
            finally
            {
            }
        }
        else if (work.Type == FilerWork.WorkType.DeleteEmptyDirectory ||
            work.Type == FilerWork.WorkType.DeleteDirectory)
        {// Delete directory recursively
            if (work.Type == FilerWork.WorkType.DeleteEmptyDirectory &&
                StorageHelper.ContainsAnyFile(filePath))
            {// Directory is not empty
                work.Result = CrystalResult.FileOperationError;
                return;
            }

            try
            {
                Directory.Delete(filePath, true);
                work.Result = CrystalResult.Success;
            }
            catch
            {
                work.Result = CrystalResult.FileOperationError;
            }
        }
        else if (work.Type == FilerWork.WorkType.List)
        {// List
            var list = new List<PathInformation>();
            try
            {
                string directory = string.Empty;
                string prefix = string.Empty;

                /*if (Directory.Exists(filePath))
                {// filePath is a directory
                    directory = filePath;
                }
                else
                {// "Directory/Prefix"
                    (directory, prefix) = PathHelper.PathToDirectoryAndFile(filePath);
                }*/

                (directory, prefix) = StorageHelper.PathToDirectoryAndFile(filePath);
                var directoryInfo = new DirectoryInfo(directory);
                foreach (var x in directoryInfo.EnumerateFileSystemInfos())
                {
                    if (string.IsNullOrEmpty(prefix) || x.Name.StartsWith(prefix))
                    {
                        if (x is FileInfo fi)
                        {
                            list.Add(new(fi.FullName, fi.Length));
                        }
                        else if (x is DirectoryInfo di)
                        {
                            list.Add(new(di.FullName));
                        }
                    }
                }
            }
            catch
            {
            }

            work.OutputObject = list;
        }
    }

    #region FieldAndProperty

    bool IRawFiler.SupportPartialWrite => true;

    private ILogger? logger;
    private ConcurrentDictionary<string, bool> checkedPath = new();

    #endregion

    async Task<CrystalResult> IRawFiler.PrepareAndCheck(PrepareParam param, PathConfiguration configuration)
    {
        this.Crystalizer = param.Crystalizer;
        if (this.Crystalizer.EnableFilerLogger)
        {
            this.logger ??= this.Crystalizer.UnitLogger.GetLogger<LocalFiler>();
        }

        var directoryPath = Path.GetDirectoryName(PathHelper.GetRootedFile(this.Crystalizer.RootDirectory, configuration.Path));
        if (directoryPath is null)
        {
            return CrystalResult.NoAccess;
        }

        if (!this.checkedPath.TryGetValue(directoryPath, out var accessible))
        {
            Directory.CreateDirectory(directoryPath);
            accessible = StorageHelper.IsDirectoryWritable(directoryPath);
            this.checkedPath.TryAdd(directoryPath, accessible);

            if (!accessible)
            {
                this.logger?.TryGet(LogLevel.Fatal)?.Log(CrystalDataHashed.LocalFiler.FailedToAccess, directoryPath);
            }
        }

        if (!accessible)
        {
            return CrystalResult.NoAccess;
        }

        return CrystalResult.Success;
    }

    public override string ToString()
        => $"LocalFiler";

    private static (bool Success, string RootedPath) CheckPath(Crystalizer crystalizer, string file)
    {
        string rootedPath = string.Empty;
        try
        {
            if (Path.IsPathRooted(file))
            {
                rootedPath = file;
            }
            else
            {
                rootedPath = Path.Combine(crystalizer.RootDirectory, file);
            }

            Directory.CreateDirectory(rootedPath);
            return (true, rootedPath);
        }
        catch
        {
        }

        return (false, rootedPath);
    }
}
