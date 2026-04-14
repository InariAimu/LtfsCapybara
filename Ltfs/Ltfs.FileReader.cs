using System.IO.Hashing;
using System.Security.Cryptography;

using Ltfs.Index;
using Ltfs.Tasks;
using Ltfs.Utils;

using TapeDrive;

namespace Ltfs;

public partial class Ltfs
{
    public ReadTaskExistingFileMode ReadTaskExistingFileMode { get; set; } = ReadTaskExistingFileMode.Ignore;

    public Task<bool> PerformReadTasks()
    {
        return Commit(LtfsTaskQueueType.Read);
    }

    public Task<bool> PerformVerifyTasks()
    {
        return Commit(LtfsTaskQueueType.Verify);
    }

    private async Task<bool> PerformReadTasks(IReadOnlyList<ReadTask> readTasks, ReadTaskExistingFileMode existingFileMode)
    {
        if (readTasks.Count == 0)
            return true;

        var label = LtfsLabelA ?? throw new InvalidOperationException("LTFS label is not loaded.");
        _ = label.Blocksize;

        var orderedTasks = OrderTasksByTapeLocation(readTasks, ResolveReadSourceFile);

        ulong totalBytesToRead = orderedTasks.Aggregate(0ul, (total, task) => total + ResolveReadSourceFile(task).Length);
        ulong totalRead = 0;
        long currentReadBytes = 0;
        long currentReadTotalBytes = 0;
        string? currentReadPath = null;
        int completedItems = 0;

        Logger.Info($"Starting read of {orderedTasks.Length} files...");

        using var progressCts = new CancellationTokenSource();
        var progressTask = Task.Run(() => RunProgressMonitor(
            LtfsTaskQueueType.Read,
            orderedTasks.Length,
            () => Volatile.Read(ref completedItems),
            () => totalRead + (ulong)Math.Max(0, Volatile.Read(ref currentReadBytes)),
            totalBytesToRead,
            () => currentReadPath,
            () => Volatile.Read(ref currentReadBytes),
            () => Volatile.Read(ref currentReadTotalBytes),
            progressCts.Token));

        var hasErrors = false;
        try
        {
            foreach (var task in orderedTasks)
            {
                MarkTaskRunning(task);
                currentReadPath = task.TargetPath;

                try
                {
                    ResetReadTaskResult(task);
                    while (true)
                    {
                        try
                        {
                            var sourceFile = ResolveReadSourceFile(task);
                            Interlocked.Exchange(ref currentReadBytes, 0);
                            Interlocked.Exchange(ref currentReadTotalBytes, (long)sourceFile.Length);
                            var readResult = await ReadFileToLocalAsync(
                                task.TargetPath,
                                sourceFile,
                                existingFileMode,
                                bytesRead => Interlocked.Add(ref currentReadBytes, bytesRead));
                            totalRead += sourceFile.Length;

                            if (readResult == ReadFileResult.Skipped)
                            {
                                Logger.Info($"Skipped existing file: {task.TargetPath}");
                            }

                            break;
                        }
                        catch (ReadIntegrityValidationException ex)
                        {
                            task.IntegrityCheckFailed = true;
                            task.FailureMessage = ex.Message;
                            task.PreservedTargetPath = ex.PreservedTargetPath;
                            Logger.Error($"Integrity validation failed for {task.SourcePath}: {ex.Message}");
                            MarkTaskFailed(task);
                            hasErrors = true;
                            break;
                        }
                        catch (LocalReadTargetIOException ex)
                        {
                            var incident = new TapeDriveIncident
                            {
                                Source = TapeDriveIncidentSource.LocalFileSystem,
                                Severity = TapeDriveIncidentSeverity.Warning,
                                Action = TapeDriveIncidentAction.PauseCurrentTasks,
                                Message = $"Target filesystem I/O error while reading '{task.SourcePath}'.",
                                Detail = $"Target path: {ex.TargetPath}. {ex.Message}",
                            };

                            if (ResolveTapeDriveIncident(incident) == TapeDriveIncidentResolution.Continue)
                            {
                                Logger.Warn($"Retrying read of {task.SourcePath} after target filesystem issue on {ex.TargetPath}.");
                                continue;
                            }

                            MarkTaskCancelled(task);
                            throw new TapeDriveCommandException(incident);
                        }
                    }

                    MarkTaskCompleted(task);
                    MarkTaskCommitted(task);
                    Interlocked.Increment(ref completedItems);
                }
                catch (TapeDriveCommandException)
                {
                    MarkTaskCancelled(task);
                    throw;
                }
                catch (Exception ex)
                {
                    task.FailureMessage = ex.Message;
                    Logger.Error($"Error reading file {task.SourcePath} to {task.TargetPath}: {ex.Message}");
                    MarkTaskFailed(task);
                    hasErrors = true;
                }
                finally
                {
                    currentReadPath = null;
                    Interlocked.Exchange(ref currentReadBytes, 0);
                    Interlocked.Exchange(ref currentReadTotalBytes, 0);
                }

                if (totalBytesToRead > 0)
                {
                    Logger.Debug($"{FileSize.FormatSize(totalRead)} / {FileSize.FormatSize(totalBytesToRead)} read Progress: {totalRead * 100 / totalBytesToRead}%");
                }
            }

            return !hasErrors;
        }
        finally
        {
            progressCts.Cancel();
            try { await progressTask; } catch { }

            long lastTicks = DateTime.UtcNow.Ticks;
            ulong lastProcessed = totalRead;
            PublishProgress(BuildProgressSnapshot(
                LtfsTaskQueueType.Read,
                orderedTasks.Length,
                Volatile.Read(ref completedItems),
                totalRead,
                totalBytesToRead,
                null,
                0,
                0,
                IsScsiMetricsSamplingEnabled() ? _lastTapePerformanceSnapshot : null,
                null,
                lastTicks,
                ref lastTicks,
                ref lastProcessed,
                isCompleted: true));
        }
    }

    private async Task<bool> PerformVerifyTasks(IReadOnlyList<VerifyTask> verifyTasks)
    {
        if (verifyTasks.Count == 0)
            return true;

        var label = LtfsLabelA ?? throw new InvalidOperationException("LTFS label is not loaded.");
        _ = label.Blocksize;

        var orderedTasks = OrderTasksByTapeLocation(verifyTasks, ResolveVerifySourceFile);

        ulong totalBytesToVerify = orderedTasks.Aggregate(0ul, (total, task) => total + ResolveVerifySourceFile(task).Length);
        ulong totalVerified = 0;
        long currentVerifyBytes = 0;
        long currentVerifyTotalBytes = 0;
        string? currentVerifyPath = null;
        int completedItems = 0;

        Logger.Info($"Starting verification of {orderedTasks.Length} files...");

        using var progressCts = new CancellationTokenSource();
        var progressTask = Task.Run(() => RunProgressMonitor(
            LtfsTaskQueueType.Verify,
            orderedTasks.Length,
            () => Volatile.Read(ref completedItems),
            () => totalVerified + (ulong)Math.Max(0, Volatile.Read(ref currentVerifyBytes)),
            totalBytesToVerify,
            () => currentVerifyPath,
            () => Volatile.Read(ref currentVerifyBytes),
            () => Volatile.Read(ref currentVerifyTotalBytes),
            progressCts.Token));

        var hasErrors = false;
        try
        {
            foreach (var task in orderedTasks)
            {
                MarkTaskRunning(task);
                ResetVerifyTaskResult(task);
                currentVerifyPath = task.SourcePath;

                try
                {
                    var sourceFile = ResolveVerifySourceFile(task);
                    Interlocked.Exchange(ref currentVerifyBytes, 0);
                    Interlocked.Exchange(ref currentVerifyTotalBytes, (long)sourceFile.Length);
                    if (sourceFile.Length == 0)
                    {
                        MarkVerifyTaskSkipped(task, "Skipped zero-length file.");
                        MarkTaskCompleted(task);
                        MarkTaskCommitted(task);
                        Interlocked.Increment(ref completedItems);
                    }
                    else
                    {
                        var expectedCrc64 = GetExpectedVerifyCrc64(sourceFile);
                        if (expectedCrc64 is null)
                        {
                            MarkVerifyTaskSkipped(task, "Skipped file without CRC64 hash.");
                            MarkTaskCompleted(task);
                            MarkTaskCommitted(task);
                            Interlocked.Increment(ref completedItems);
                        }
                        else
                        {
                            var verifyResult = await VerifyFileAsync(
                                sourceFile,
                                expectedCrc64,
                                bytesVerified => Interlocked.Add(ref currentVerifyBytes, bytesVerified));
                            task.ExpectedCrc64 = verifyResult.ExpectedCrc64;
                            task.ActualCrc64 = verifyResult.ActualCrc64;
                            task.VerificationPassed = verifyResult.Passed;
                            task.VerificationMessage = verifyResult.Message;
                            totalVerified += sourceFile.Length;

                            if (verifyResult.Passed)
                            {
                                MarkTaskCompleted(task);
                                MarkTaskCommitted(task);
                                Interlocked.Increment(ref completedItems);
                            }
                            else
                            {
                                MarkTaskFailed(task);
                                hasErrors = true;
                            }
                        }
                    }
                }
                catch (TapeDriveCommandException)
                {
                    task.VerificationPassed = false;
                    task.VerificationMessage = "Verification aborted by tape drive incident.";
                    MarkTaskCancelled(task);
                    throw;
                }
                catch (Exception ex)
                {
                    task.VerificationPassed = false;
                    task.VerificationMessage = ex.Message;
                    Logger.Error($"Error verifying file {task.SourcePath}: {ex.Message}");
                    MarkTaskFailed(task);
                    hasErrors = true;
                }
                finally
                {
                    currentVerifyPath = null;
                    Interlocked.Exchange(ref currentVerifyBytes, 0);
                    Interlocked.Exchange(ref currentVerifyTotalBytes, 0);
                }

                if (totalBytesToVerify > 0)
                {
                    Logger.Debug($"{FileSize.FormatSize(totalVerified)} / {FileSize.FormatSize(totalBytesToVerify)} verification Progress: {totalVerified * 100 / totalBytesToVerify}%");
                }
            }

            return !hasErrors;
        }
        finally
        {
            progressCts.Cancel();
            try { await progressTask; } catch { }

            long lastTicks = DateTime.UtcNow.Ticks;
            ulong lastProcessed = totalVerified;
            PublishProgress(BuildProgressSnapshot(
                LtfsTaskQueueType.Verify,
                orderedTasks.Length,
                Volatile.Read(ref completedItems),
                totalVerified,
                totalBytesToVerify,
                null,
                0,
                0,
                IsScsiMetricsSamplingEnabled() ? _lastTapePerformanceSnapshot : null,
                null,
                lastTicks,
                ref lastTicks,
                ref lastProcessed,
                isCompleted: true));
        }
    }

    private async Task<ReadFileResult> ReadFileToLocalAsync(string targetPath, LtfsFile file, ReadTaskExistingFileMode existingFileMode, Action<int>? onBytesRead = null)
    {
        var label = LtfsLabelA ?? throw new InvalidOperationException("LTFS label is not loaded.");
        var resolvedTargetPath = Path.GetFullPath(targetPath);
        InvalidDataException? integrityValidationException = null;

        if (File.Exists(resolvedTargetPath))
        {
            if (existingFileMode == ReadTaskExistingFileMode.Ignore)
                return ReadFileResult.Skipped;
        }

        if (Directory.Exists(resolvedTargetPath))
            throw new LocalReadTargetIOException(resolvedTargetPath, $"Target path is a directory: {resolvedTargetPath}");

        var targetDirectory = Path.GetDirectoryName(resolvedTargetPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            try
            {
                Directory.CreateDirectory(targetDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new LocalReadTargetIOException(resolvedTargetPath, ex.Message, ex);
            }
        }

        var tempPath = BuildTemporaryReadPath(resolvedTargetPath);
        DeleteFileIfExists(tempPath);

        try
        {
            await using (var fs = CreateReadTargetStream(tempPath, label.Blocksize, resolvedTargetPath))
            using (var validator = ReadHashValidator.Create(file))
            {
                await ReadTapeFileAsync(file, async (fileOffset, chunk) =>
                {
                    try
                    {
                        fs.Seek(fileOffset, SeekOrigin.Begin);
                        await fs.WriteAsync(chunk);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        throw new LocalReadTargetIOException(resolvedTargetPath, ex.Message, ex);
                    }

                    validator?.Append(chunk.Span);
                    onBytesRead?.Invoke(chunk.Length);
                });

                try
                {
                    await fs.FlushAsync();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new LocalReadTargetIOException(resolvedTargetPath, ex.Message, ex);
                }

                try
                {
                    validator?.Validate();
                }
                catch (InvalidDataException ex)
                {
                    integrityValidationException = ex;
                }
            }

            MoveReadFileIntoPlace(tempPath, resolvedTargetPath, existingFileMode);

            if (integrityValidationException is not null)
            {
                throw new ReadIntegrityValidationException(resolvedTargetPath, integrityValidationException.Message, integrityValidationException);
            }

            ApplyReadFileAttributes(resolvedTargetPath, file);
            return ReadFileResult.Success;
        }
        catch (ReadIntegrityValidationException)
        {
            throw;
        }
        catch (Exception) when (TryCleanupPartialRead(tempPath))
        {
            throw;
        }
    }

    private static string PreserveInvalidReadFile(string tempPath, string resolvedTargetPath, ReadTaskExistingFileMode existingFileMode)
    {
        MoveReadFileIntoPlace(tempPath, resolvedTargetPath, existingFileMode);
        return resolvedTargetPath;
    }

    private static FileStream CreateReadTargetStream(string tempPath, int blockSize, string targetPath)
    {
        try
        {
            return new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: blockSize, useAsync: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LocalReadTargetIOException(targetPath, ex.Message, ex);
        }
    }

    private static void MoveReadFileIntoPlace(string tempPath, string resolvedTargetPath, ReadTaskExistingFileMode existingFileMode)
    {
        try
        {
            if (existingFileMode == ReadTaskExistingFileMode.Overwrite && File.Exists(resolvedTargetPath))
            {
                File.Move(tempPath, resolvedTargetPath, overwrite: true);
            }
            else
            {
                File.Move(tempPath, resolvedTargetPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LocalReadTargetIOException(resolvedTargetPath, ex.Message, ex);
        }
    }

    private async Task<VerifyResult> VerifyFileAsync(LtfsFile file, string expectedCrc64, Action<int>? onBytesVerified = null)
    {
        var crc64 = new Crc64();
        await ReadTapeFileAsync(file, (_, chunk) =>
        {
            crc64.Append(chunk.Span);
            onBytesVerified?.Invoke(chunk.Length);
            return Task.CompletedTask;
        });
        var actualCrc64 = crc64.GetCurrentHashAsUInt64().ToString("X16");
        var passed = string.Equals(actualCrc64, expectedCrc64, StringComparison.OrdinalIgnoreCase);
        var message = passed
            ? "CRC64 verification passed."
            : $"CRC64 mismatch. Expected {expectedCrc64}, got {actualCrc64}.";

        return new VerifyResult(expectedCrc64, actualCrc64, passed, message);
    }

    private static string BuildTemporaryReadPath(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath) ?? Path.GetTempPath();
        var fileName = Path.GetFileName(targetPath);
        return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.part");
    }

    private static bool TryCleanupPartialRead(string tempPath)
    {
        DeleteFileIfExists(tempPath);
        return true;
    }

    private static void DeleteFileIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void ResetReadTaskResult(ReadTask task)
    {
        task.IntegrityCheckFailed = false;
        task.FailureMessage = null;
        task.PreservedTargetPath = null;
    }

    private void ApplyReadFileAttributes(string fileName, LtfsFile file)
    {
        try
        {
            var fi = new FileInfo(fileName)
            {
                CreationTimeUtc = file.CreationTime,
                LastWriteTimeUtc = file.ModifyTime,
                LastAccessTimeUtc = file.AccessTime,
                IsReadOnly = file.ReadOnly
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LocalReadTargetIOException(fileName, ex.Message, ex);
        }
    }

    private sealed class LocalReadTargetIOException(string targetPath, string message, Exception? innerException = null)
        : IOException(message, innerException)
    {
        public string TargetPath { get; } = targetPath;
    }

    private sealed class ReadIntegrityValidationException(string preservedTargetPath, string message, Exception? innerException = null)
        : IOException(message, innerException)
    {
        public string PreservedTargetPath { get; } = preservedTargetPath;
    }

    private byte[] ReadTapeBlock(uint blockLength)
    {
        byte[]? data = null;
        bool readSucceeded = false;
        while (!readSucceeded)
        {
            data = _tapeDrive.ReadBlock(blockLength, true);
            var sense = _tapeDrive.Sense;
            if (((sense[2] >> 6) & 1) == 1)
            {
                readSucceeded = true;
            }
            else if ((sense[2] & 0x0f) != 0)
            {
                throw new IOException("SCSI sense error while reading tape.");
            }
            else
            {
                readSucceeded = true;
            }
        }

        return data ?? throw new IOException("Tape returned no data.");
    }

    private async Task ReadTapeFileAsync(LtfsFile file, Func<long, ReadOnlyMemory<byte>, Task>? onChunk)
    {
        if (file.Length == 0)
            return;

        var label = LtfsLabelA ?? throw new InvalidOperationException("LTFS label is not loaded.");
        var extents = file.ExtentInfo?.Extent;
        if (extents is not { Length: > 0 })
            throw new IOException("LTFS file has no readable extents.");

        foreach (var extent in OrderReadExtents(extents))
        {
            _tapeDrive.Locate(extent.StartBlock, PartitionToNumber(extent.Partition), LocateType.Block);

            ulong remainingBytes = extent.ByteCount + extent.ByteOffset;
            ulong currentByteOffset = extent.ByteOffset;
            ulong bytesReadFromExtent = 0;
            while (remainingBytes > 0)
            {
                uint blockLength = (uint)Math.Min((ulong)label.Blocksize, remainingBytes);
                var data = ReadTapeBlock(blockLength);
                if (data.Length != blockLength || blockLength == 0)
                    throw new IOException("Block length mismatch or zero.");

                var readOffset = (int)currentByteOffset;
                var readLength = (int)(blockLength - currentByteOffset);
                if (readLength < 0)
                    throw new IOException("Invalid LTFS extent byte offsets.");

                var chunk = data.AsMemory(readOffset, readLength);
                if (onChunk is not null)
                {
                    await onChunk((long)(extent.FileOffset + bytesReadFromExtent), chunk);
                }

                bytesReadFromExtent += (ulong)readLength;
                remainingBytes -= blockLength;
                currentByteOffset = 0;
            }
        }
    }

    private LtfsFile ResolveReadSourceFile(ReadTask task)
    {
        if (task.SourceFile is not null)
            return task.SourceFile;

        var normalizedSourcePath = LtfsIndexOperations.NormalizePath(task.SourcePath, allowRoot: false);
        var sourceFile = LtfsIndexOperations.FindFile(GetLatestIndex(), normalizedSourcePath)
            ?? throw new FileNotFoundException($"LTFS source file not found: {normalizedSourcePath}");

        task.SourcePath = normalizedSourcePath;
        task.SourceFile = sourceFile;
        return sourceFile;
    }

    private LtfsFile ResolveVerifySourceFile(VerifyTask task)
    {
        if (task.SourceFile is not null)
            return task.SourceFile;

        var normalizedSourcePath = LtfsIndexOperations.NormalizePath(task.SourcePath, allowRoot: false);
        var sourceFile = LtfsIndexOperations.FindFile(GetLatestIndex(), normalizedSourcePath)
            ?? throw new FileNotFoundException($"LTFS source file not found: {normalizedSourcePath}");

        task.SourcePath = normalizedSourcePath;
        task.SourceFile = sourceFile;
        return sourceFile;
    }

    private static void ResetVerifyTaskResult(VerifyTask task)
    {
        task.VerificationSkipped = false;
        task.VerificationPassed = null;
        task.ExpectedCrc64 = null;
        task.ActualCrc64 = null;
        task.VerificationMessage = null;
    }

    private static void MarkVerifyTaskSkipped(VerifyTask task, string message)
    {
        task.VerificationSkipped = true;
        task.VerificationPassed = null;
        task.VerificationMessage = message;
    }

    private static string? GetExpectedVerifyCrc64(LtfsFile file)
    {
        return NormalizeHashValue(file.ExtendedAttributes?["ltfs.hash.crc64sum"]);
    }

    private TTask[] OrderTasksByTapeLocation<TTask>(IEnumerable<TTask> tasks, Func<TTask, LtfsFile> resolveSourceFile)
        where TTask : TaskBase
    {
        return tasks
            .Select(task => new
            {
                Task = task,
                Extent = GetFirstExtent(resolveSourceFile(task)),
            })
            .OrderBy(item => item.Extent is null ? byte.MaxValue : PartitionToNumber(item.Extent.Partition))
            .ThenBy(item => item.Extent?.StartBlock ?? ulong.MaxValue)
            .ThenBy(item => item.Task.SequenceNumber)
            .Select(item => item.Task)
            .ToArray();
    }

    private static Extent? GetFirstExtent(LtfsFile file)
    {
        return file.ExtentInfo?.Extent?
            .OrderBy(extent => extent.FileOffset)
            .ThenBy(extent => extent.StartBlock)
            .FirstOrDefault();
    }

    private IEnumerable<Extent> OrderReadExtents(IEnumerable<Extent> extents)
    {
        return extents
            .OrderBy(extent => PartitionToNumber(extent.Partition))
            .ThenBy(extent => extent.StartBlock)
            .ThenBy(extent => extent.FileOffset);
    }

    private enum ReadFileResult
    {
        Success,
        Skipped,
    }

    private sealed record VerifyResult(string ExpectedCrc64, string ActualCrc64, bool Passed, string Message);

    private sealed class ReadHashValidator : IDisposable
    {
        private readonly string? expectedCrc64;
        private readonly string? expectedSha1;
        private readonly Crc64? crc64;
        private readonly IncrementalHash? sha1;

        private ReadHashValidator(string? expectedCrc64, string? expectedSha1)
        {
            this.expectedCrc64 = NormalizeHashValue(expectedCrc64);
            this.expectedSha1 = NormalizeHashValue(expectedSha1);

            if (!string.IsNullOrEmpty(this.expectedCrc64))
            {
                crc64 = new Crc64();
            }

            if (!string.IsNullOrEmpty(this.expectedSha1))
            {
                sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            }
        }

        public static ReadHashValidator? Create(LtfsFile file, bool requireExpectedHash = false)
        {
            var expectedCrc64 = file.ExtendedAttributes?["ltfs.hash.crc64sum"];
            var expectedSha1 = file.ExtendedAttributes?["ltfs.hash.sha1sum"];
            if (string.IsNullOrWhiteSpace(expectedCrc64) && string.IsNullOrWhiteSpace(expectedSha1))
            {
                if (requireExpectedHash)
                    throw new InvalidDataException("LTFS file does not contain a supported hash for verification.");

                return null;
            }

            return new ReadHashValidator(expectedCrc64, expectedSha1);
        }

        public void Append(ReadOnlySpan<byte> data)
        {
            crc64?.Append(data);
            sha1?.AppendData(data);
        }

        public void Validate()
        {
            if (crc64 is not null)
            {
                var actualCrc64 = crc64.GetCurrentHashAsUInt64().ToString("X16");
                if (!string.Equals(actualCrc64, expectedCrc64, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"CRC64 mismatch. Expected {expectedCrc64}, got {actualCrc64}.");
            }

            if (sha1 is not null)
            {
                var actualSha1 = Convert.ToHexString(sha1.GetHashAndReset());
                if (!string.Equals(actualSha1, expectedSha1, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"SHA-1 mismatch. Expected {expectedSha1}, got {actualSha1}.");
            }
        }

        public void Dispose()
        {
            sha1?.Dispose();
        }

    }

    private static string? NormalizeHashValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }
}