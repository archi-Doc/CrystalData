// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Collections.Concurrent;
using Amazon.S3;
using Amazon.S3.Model;
using CrystalData.Results;

#pragma warning disable SA1124 // Do not use regions

namespace CrystalData.Filer;

public class S3Filer : FilerBase, IRawFiler
{// Vault: S3Bucket/BucketName "AccessKeyId=SecretAccessKey"
    private const string WriteTestFile = "Write.test";

    public S3Filer()
        : base(Process)
    {
    }

    public S3Filer(string bucket)
        : this()
    {
        this.bucket = bucket;
    }

    /*public static AddStorageResult Check(GroupStorage storageGroup, string bucket, string path)
    {
        if (!storageGroup.Crystalizer.StorageKey.TryGetKey(bucket, out var accessKeyPair))
        {
            return AddStorageResult.NoStorageKey;
        }

        return AddStorageResult.Success;
    }*/

    public override string ToString()
        => $"S3Filer Bucket: {this.bucket}";

    #region FieldAndProperty

    private ILogger? logger;
    private string bucket = string.Empty;
    private AmazonS3Client? client;
    private ConcurrentDictionary<string, bool> checkedPath = new();

    #endregion

    public static async Task Process(TaskWorker<FilerWork> w, FilerWork work)
    {
        var worker = (S3Filer)w;
        if (worker.client == null)
        {
            work.Result = CrystalResult.NotPrepared;
            return;
        }

        var tryCount = 0;
        work.Result = CrystalResult.Started;
        var filePath = work.Path;
        if (work.Type == FilerWork.WorkType.Write)
        {// Write
            try
            {
TryWrite:
                tryCount++;
                if (tryCount > 1)
                {
                    work.Result = CrystalResult.FileOperationError;
                    return;
                }

                try
                {
                    using (var ms = new ReadOnlyMemoryStream(work.WriteData.Memory))
                    {
                        var request = new Amazon.S3.Model.PutObjectRequest() { BucketName = worker.bucket, Key = filePath, InputStream = ms, };
                        var response = await worker.client.PutObjectAsync(request, worker.CancellationToken).ConfigureAwait(false);
                        if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                        {
                            worker.logger?.TryGet(LogLevel.Debug)?.Log($"Written {filePath}, {work.WriteData.Memory.Length}");
                            work.Result = CrystalResult.Success;
                            return;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    work.Result = CrystalResult.Aborted;
                    return;
                }
                catch
                {
                }

                // Retry
                worker.logger?.TryGet(LogLevel.Warning)?.Log($"Retry {filePath}");
                goto TryWrite;
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
                var request = new Amazon.S3.Model.GetObjectRequest() { BucketName = worker.bucket, Key = filePath, };
                if (work.Length > 0)
                {
                    request.ByteRange = new(work.Offset, work.Length);
                }

                var response = await worker.client.GetObjectAsync(request, worker.CancellationToken).ConfigureAwait(false);
                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK ||
                    response.HttpStatusCode == System.Net.HttpStatusCode.PartialContent)
                {
                    using (var ms = new MemoryStream())
                    {
                        response.ResponseStream.CopyTo(ms);
                        work.Result = CrystalResult.Success;
                        work.ReadData = BytePool.RentMemory.CreateFrom(ms.ToArray());
                        worker.logger?.TryGet(LogLevel.Debug)?.Log($"Read {filePath}, {work.ReadData.Memory.Length}");
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                work.Result = CrystalResult.Aborted;
                return;
            }
            catch
            {
            }
            finally
            {
            }

            work.Result = CrystalResult.FileOperationError;
            worker.logger?.TryGet(LogLevel.Error)?.Log($"Read exception {filePath}");
        }
        else if (work.Type == FilerWork.WorkType.Delete)
        {// Delete
            try
            {
                var request = new Amazon.S3.Model.DeleteObjectRequest() { BucketName = worker.bucket, Key = filePath, };
                var response = await worker.client.DeleteObjectAsync(request, worker.CancellationToken).ConfigureAwait(false);
                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    work.Result = CrystalResult.Success;
                }
            }
            catch
            {
            }

            work.Result = CrystalResult.FileOperationError;
        }
        else if (work.Type == FilerWork.WorkType.DeleteEmptyDirectory)
        {// Delete empty directory
        }
        else if (work.Type == FilerWork.WorkType.DeleteDirectory)
        {// Delete directory recursively
            if (!filePath.EndsWith(StorageHelper.Slash))
            {
                filePath += StorageHelper.Slash;
            }

            while (true)
            {
                var listRequest = new ListObjectsV2Request() { BucketName = worker.bucket, Prefix = filePath, };
                var listResponse = await worker.client.ListObjectsV2Async(listRequest).ConfigureAwait(false);
                if (listResponse.HttpStatusCode != System.Net.HttpStatusCode.OK)
                {
                    work.Result = CrystalResult.FileOperationError;
                    return;
                }

                if (listResponse.KeyCount == 0)
                {// No file left
                    work.Result = CrystalResult.Success;
                    return;
                }

                var deleteRequest = new DeleteObjectsRequest() { BucketName = worker.bucket, };
                foreach (var x in listResponse.S3Objects)
                {
                    deleteRequest.AddKey(x.Key);
                }

                var deleteResponse = await worker.client.DeleteObjectsAsync(deleteRequest).ConfigureAwait(false);
                if (deleteResponse.HttpStatusCode != System.Net.HttpStatusCode.OK)
                {
                    work.Result = CrystalResult.FileOperationError;
                    return;
                }
            }
        }
        else if (work.Type == FilerWork.WorkType.List)
        {// List
            var list = new List<PathInformation>();
            /*if (!filePath.EndsWith(PathHelper.Slash) && !string.IsNullOrEmpty(filePath))
            {
                filePath += PathHelper.Slash;
            }*/

            try
            {
                string? continuationToken = null;
RepeatList:
                var request = new Amazon.S3.Model.ListObjectsV2Request() { BucketName = worker.bucket, Prefix = filePath, Delimiter = StorageHelper.SlashString, ContinuationToken = continuationToken, };

                var response = await worker.client.ListObjectsV2Async(request, worker.CancellationToken).ConfigureAwait(false);
                foreach (var x in response.S3Objects)
                {
                    list.Add(new(x.Key, x.Size ?? 0));
                }

                foreach (var x in response.CommonPrefixes)
                {
                    list.Add(new(x));
                }

                if (response.IsTruncated == true)
                {
                    continuationToken = response.NextContinuationToken;
                    goto RepeatList;
                }
            }
            catch
            {
            }

            work.OutputObject = list;
        }

        return;
    }

    bool IRawFiler.SupportPartialWrite => false;

    async Task<CrystalResult> IRawFiler.PrepareAndCheck(PrepareParam param, PathConfiguration configuration)
    {
        var directoryPath = string.Empty;
        this.Crystalizer = param.Crystalizer;
        if (this.Crystalizer.EnableFilerLogger)
        {
            this.logger ??= this.Crystalizer.UnitLogger.GetLogger<S3Filer>();
        }

        if (!this.Crystalizer.StorageKey.TryGetKey(this.bucket, out var accessKeyPair))
        {// No access key
            this.logger?.TryGet(LogLevel.Fatal)?.Log(CrystalDataHashed.S3Filer.NoAccessKey, this.bucket);
            return CrystalResult.NoAccess;
        }

        if (this.client == null)
        {
            try
            {
                this.client = new AmazonS3Client(accessKeyPair.AccessKeyId, accessKeyPair.SecretAccessKey);
            }
            catch
            {
                goto NoAccess;
            }
        }

        // Write test.
        directoryPath = configuration is FileConfiguration ? Path.GetDirectoryName(configuration.Path) ?? string.Empty : configuration.Path;
        if (!this.checkedPath.TryGetValue(directoryPath, out var accessible))
        {
            try
            {
                var path = StorageHelper.CombineWithSlash(directoryPath, WriteTestFile);
                using (var ms = new MemoryStream())
                {
                    var request = new Amazon.S3.Model.PutObjectRequest() { BucketName = this.bucket, Key = path, InputStream = ms, };
                    var response = await this.client.PutObjectAsync(request).ConfigureAwait(false);
                    if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
                    {
                        this.checkedPath.TryAdd(directoryPath, false);
                        goto NoAccess;
                    }
                }
            }
            catch
            {
                this.checkedPath.TryAdd(directoryPath, false);
                goto NoAccess;
            }

            this.checkedPath.TryAdd(directoryPath, true);
        }

        return CrystalResult.Success;

NoAccess:
        this.logger?.TryGet(LogLevel.Fatal)?.Log(CrystalDataHashed.S3Filer.FailedToAccess, this.bucket, directoryPath);
        return CrystalResult.NoAccess;
    }

    async Task IRawFiler.FlushAsync(bool terminate)
    {
        await this.WaitForCompletionAsync().ConfigureAwait(false);
        if (terminate)
        {
            this.client?.Dispose();
            this.client = null;
            this.Dispose();
        }
    }
}
