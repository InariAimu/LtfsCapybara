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

    private async Task<bool> PerformReadTasks(IReadOnlyList<ReadTask> readTasks, ReadTaskExistingFileMode existingFileMode)
    {
        if (readTasks.Count == 0)
            return true;

        var label = LtfsLabelA ?? throw new InvalidOperationException("LTFS label is not loaded.");
        _ = label.Blocksize;

        var orderedTasks = readTasks
            .Select(task => new
            {
                Task = task,
                Extent = GetFirstExtent(ResolveReadSourceFile(task)),
            })
            .OrderBy(item => item.Extent is null ? byte.MaxValue : PartitionToNumber(item.Extent.Partition))
            .ThenBy(item => item.Extent?.StartBlock ?? ulong.MaxValue)
            .ThenBy(item => item.Task.SequenceNumber)
            .Select(item => item.Task)
            .ToArray();

        ulong totalBytesToRead = orderedTasks.Aggregate(0ul, (total, task) => total + ResolveReadSourceFile(task).Length);
        ulong totalRead = 0;

        Logger.Info($"Starting read of {orderedTasks.Length} files...");

        var hasErrors = false;
        foreach (var task in orderedTasks)
        {
            MarkTaskRunning(task);

            try
            {
                var sourceFile = ResolveReadSourceFile(task);
                var readResult = await ReadFileToLocalAsync(task.TargetPath, sourceFile, existingFileMode);
                totalRead += sourceFile.Length;

                if (readResult == ReadFileResult.Skipped)
                {
                    Logger.Info($"Skipped existing file: {task.TargetPath}");
                }

                MarkTaskCompleted(task);
                MarkTaskCommitted(task);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error reading file {task.SourcePath} to {task.TargetPath}: {ex.Message}");
                MarkTaskFailed(task);
                hasErrors = true;
            }

            if (totalBytesToRead > 0)
            {
                Logger.Debug($"{FileSize.FormatSize(totalRead)} / {FileSize.FormatSize(totalBytesToRead)} read Progress: {totalRead * 100 / totalBytesToRead}%");
            }
        }

        return !hasErrors;
    }

    private async Task<ReadFileResult> ReadFileToLocalAsync(string targetPath, LtfsFile file, ReadTaskExistingFileMode existingFileMode)
    {
        var label = LtfsLabelA ?? throw new InvalidOperationException("LTFS label is not loaded.");
        var resolvedTargetPath = Path.GetFullPath(targetPath);

        if (File.Exists(resolvedTargetPath))
        {
            if (existingFileMode == ReadTaskExistingFileMode.Ignore)
                return ReadFileResult.Skipped;
        }

        if (Directory.Exists(resolvedTargetPath))
            throw new IOException($"Target path is a directory: {resolvedTargetPath}");

        var targetDirectory = Path.GetDirectoryName(resolvedTargetPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        var tempPath = BuildTemporaryReadPath(resolvedTargetPath);
        DeleteFileIfExists(tempPath);

        try
        {
            await using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: label.Blocksize, useAsync: true))
            using (var validator = ReadHashValidator.Create(file))
            {
                if (file.Length > 0 && file.ExtentInfo?.Extent is { Length: > 0 } extents)
                {
                    foreach (var extent in OrderReadExtents(extents))
                    {
                        _tapeDrive.Locate(extent.StartBlock, PartitionToNumber(extent.Partition), LocateType.Block);
                        fs.Seek((long)extent.FileOffset, SeekOrigin.Begin);

                        ulong remainingBytes = extent.ByteCount + extent.ByteOffset;
                        ulong currentByteOffset = extent.ByteOffset;
                        while (remainingBytes > 0)
                        {
                            uint blockLength = (uint)Math.Min((ulong)label.Blocksize, remainingBytes);
                            var data = ReadTapeBlock(blockLength);
                            if (data.Length != blockLength || blockLength == 0)
                                throw new IOException("Block length mismatch or zero.");

                            var writeOffset = (int)currentByteOffset;
                            var writeLength = (int)(blockLength - currentByteOffset);
                            if (writeLength < 0)
                                throw new IOException("Invalid LTFS extent byte offsets.");

                            var chunk = data.AsMemory(writeOffset, writeLength);
                            await fs.WriteAsync(chunk);
                            validator?.Append(chunk.Span);

                            remainingBytes -= blockLength;
                            currentByteOffset = 0;
                        }
                    }
                }

                validator?.Validate();
                await fs.FlushAsync();
            }

            if (existingFileMode == ReadTaskExistingFileMode.Overwrite && File.Exists(resolvedTargetPath))
            {
                File.Move(tempPath, resolvedTargetPath, overwrite: true);
            }
            else
            {
                File.Move(tempPath, resolvedTargetPath);
            }

            ApplyReadFileAttributes(resolvedTargetPath, file);
            return ReadFileResult.Success;
        }
        catch (Exception) when (TryCleanupPartialRead(tempPath))
        {
            throw;
        }
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

    private void ApplyReadFileAttributes(string fileName, LtfsFile file)
    {
        var fi = new FileInfo(fileName)
        {
            CreationTimeUtc = file.CreationTime,
            LastWriteTimeUtc = file.ModifyTime,
            LastAccessTimeUtc = file.AccessTime,
            IsReadOnly = file.ReadOnly
        };
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

    private sealed class ReadHashValidator : IDisposable
    {
        private readonly string? expectedCrc64;
        private readonly string? expectedSha1;
        private readonly Crc64? crc64;
        private readonly IncrementalHash? sha1;

        private ReadHashValidator(string? expectedCrc64, string? expectedSha1)
        {
            this.expectedCrc64 = NormalizeHash(expectedCrc64);
            this.expectedSha1 = NormalizeHash(expectedSha1);

            if (!string.IsNullOrEmpty(this.expectedCrc64))
            {
                crc64 = new Crc64();
            }

            if (!string.IsNullOrEmpty(this.expectedSha1))
            {
                sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            }
        }

        public static ReadHashValidator? Create(LtfsFile file)
        {
            var expectedCrc64 = file.ExtendedAttributes?["ltfs.hash.crc64sum"];
            var expectedSha1 = file.ExtendedAttributes?["ltfs.hash.sha1sum"];
            if (string.IsNullOrWhiteSpace(expectedCrc64) && string.IsNullOrWhiteSpace(expectedSha1))
                return null;

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

        private static string? NormalizeHash(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        }
    }
}