using Ltfs.Index;
using Ltfs.Tasks;
using TapeDrive;

namespace Ltfs;

public partial class Ltfs
{
    protected readonly List<TaskBase> pendingWriteTasks = [];
    protected readonly List<TaskBase> pendingReadTasks = [];
    protected readonly List<TaskBase> pendingVerifyTasks = [];
    private long nextTaskSequence = 0;

    public LtfsFile? FindFile(string path)
    {
        return LtfsIndexOperations.FindFile(BuildPendingIndexView(), path);
    }

    public LtfsDirectory? FindDirectory(string path)
    {
        return LtfsIndexOperations.FindDirectory(BuildPendingIndexView(), path);
    }

    public void AddDirectory(string srcDirectory, string targetPath, bool recrusive = true)
    {
        if (!Directory.Exists(srcDirectory))
            return;

        var normalizedTargetPath = LtfsIndexOperations.NormalizePath(targetPath);
        CreateDirectory(normalizedTargetPath);

        var dirInfo = new DirectoryInfo(srcDirectory);
        var entries = dirInfo.GetFileSystemInfos();

        foreach (var entry in entries)
        {
            var relativePath = LtfsIndexOperations.NormalizePath(Path.Combine(normalizedTargetPath, entry.Name));
            if (entry is FileInfo fileInfo)
            {
                AddFile(fileInfo.FullName, relativePath);
            }
            else if (entry is DirectoryInfo subDirInfo)
            {
                if (recrusive)
                {
                    CreateDirectory(relativePath);
                    AddDirectory(subDirInfo.FullName, relativePath, recrusive);
                }
            }
        }
    }

    public void CreateDirectory(string targetPath)
    {
        var normalizedTargetPath = LtfsIndexOperations.NormalizePath(targetPath);
        if (LtfsIndexOperations.FindDirectory(BuildPendingIndexView(), normalizedTargetPath) is not null)
            return;

        RemovePendingTasks(normalizedTargetPath, includeDescendants: false);
        EnqueueTask(new CreateDirectoryTask
        {
            TargetPath = normalizedTargetPath,
        });
    }

    public void AddFile(string fileName, string targetPath)
    {
        if (!File.Exists(fileName))
            return;

        var normalizedTargetPath = LtfsIndexOperations.NormalizePath(targetPath, allowRoot: false);
        var latestIndex = GetLatestIndex();
        var effectiveIndex = BuildPendingIndexView();
        var existingBaseEntry = LtfsIndexOperations.FindEntry(latestIndex, normalizedTargetPath);
        var existingEffectiveEntry = LtfsIndexOperations.FindEntry(effectiveIndex, normalizedTargetPath);
        if (existingEffectiveEntry is LtfsDirectory)
            throw new InvalidOperationException($"Path '{normalizedTargetPath}' conflicts with an existing directory.");

        var fileInfo = new FileInfo(fileName);

        var ltfsFile = LtfsFile.Default();
        ltfsFile.Name = Path.GetFileName(normalizedTargetPath);
        ltfsFile.Length = (ulong)fileInfo.Length;
        ltfsFile.CreationTime = fileInfo.CreationTimeUtc;
        ltfsFile.ChangeTime = fileInfo.LastWriteTimeUtc;
        ltfsFile.ModifyTime = fileInfo.LastWriteTimeUtc;
        ltfsFile.AccessTime = fileInfo.LastAccessTimeUtc;
        ltfsFile.BackupTime = fileInfo.LastWriteTimeUtc;
        ltfsFile.ReadOnly = fileInfo.IsReadOnly;
        ltfsFile.FileUID = 0;

        RemovePendingWriteTask(normalizedTargetPath);

        if (existingBaseEntry is LtfsFile && existingEffectiveEntry is LtfsFile)
        {
            EnqueueReplaceDelete(normalizedTargetPath);
        }

        EnqueueTask(new WriteTask
        {
            LocalPath = fileName,
            TargetPath = normalizedTargetPath,
            LtfsTargetPath = ltfsFile,
        });
    }

    public void AddReadTask(string sourcePath, string targetPath)
    {
        var normalizedSourcePath = LtfsIndexOperations.NormalizePath(sourcePath, allowRoot: false);
        var sourceEntry = LtfsIndexOperations.FindEntry(GetLatestIndex(), normalizedSourcePath)
            ?? throw new FileNotFoundException($"LTFS source path not found: {normalizedSourcePath}");

        switch (sourceEntry)
        {
            case LtfsFile sourceFile:
                EnqueueReadTask(normalizedSourcePath, Path.GetFullPath(targetPath), sourceFile);
                break;
            case LtfsDirectory sourceDirectory:
                var targetRootPath = Path.GetFullPath(targetPath);
                foreach (var (filePath, file) in EnumerateDirectoryFiles(normalizedSourcePath, sourceDirectory))
                {
                    var relativePath = Path.GetRelativePath(normalizedSourcePath.TrimStart('/'), filePath.TrimStart('/'));
                    var relativeSegments = relativePath
                        .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var resolvedTargetPath = relativeSegments.Length == 0
                        ? targetRootPath
                        : Path.Combine([targetRootPath, .. relativeSegments]);
                    EnqueueReadTask(filePath, resolvedTargetPath, file);
                }
                break;
            default:
                throw new InvalidOperationException($"Unsupported LTFS source path type: {normalizedSourcePath}");
        }
    }

    public void AddVerifyTask(string sourcePath)
    {
        var normalizedSourcePath = LtfsIndexOperations.NormalizePath(sourcePath, allowRoot: false);
        var sourceEntry = LtfsIndexOperations.FindEntry(GetLatestIndex(), normalizedSourcePath)
            ?? throw new FileNotFoundException($"LTFS source path not found: {normalizedSourcePath}");

        switch (sourceEntry)
        {
            case LtfsFile sourceFile:
                EnqueueVerifyTask(normalizedSourcePath, sourceFile);
                break;
            case LtfsDirectory sourceDirectory:
                foreach (var (filePath, file) in EnumerateDirectoryFiles(normalizedSourcePath, sourceDirectory))
                {
                    EnqueueVerifyTask(filePath, file);
                }
                break;
            default:
                throw new InvalidOperationException($"Unsupported LTFS source path type: {normalizedSourcePath}");
        }
    }

    public void DeletePath(string targetPath)
    {
        var normalizedTargetPath = LtfsIndexOperations.NormalizePath(targetPath);
        var latestIndex = GetLatestIndex();
        var existingBaseEntry = LtfsIndexOperations.FindEntry(latestIndex, normalizedTargetPath);
        var existingEffectiveEntry = LtfsIndexOperations.FindEntry(BuildPendingIndexView(), normalizedTargetPath);
        if (existingEffectiveEntry is null)
            return;

        RemovePendingTasks(normalizedTargetPath, includeDescendants: true);

        if (existingBaseEntry is null)
            return;

        EnqueueTask(new DeleteTask
        {
            TargetPath = normalizedTargetPath,
        });
    }

    public void MovePath(string sourcePath, string targetPath)
    {
        var normalizedSourcePath = LtfsIndexOperations.NormalizePath(sourcePath, allowRoot: false);
        var normalizedTargetPath = LtfsIndexOperations.NormalizePath(targetPath, allowRoot: false);
        if (string.Equals(normalizedSourcePath, normalizedTargetPath, StringComparison.OrdinalIgnoreCase))
            return;

        if (LtfsIndexOperations.FindEntry(BuildPendingIndexView(), normalizedSourcePath) is null)
            return;

        RemovePendingTasks(normalizedSourcePath, includeDescendants: true);
        RemovePendingTasks(normalizedTargetPath, includeDescendants: true);

        EnqueueTask(new MoveTask
        {
            SourcePath = normalizedSourcePath,
            TargetPath = normalizedTargetPath,
        });
    }

    public IReadOnlyList<TaskBase> GetPendingTasks()
    {
        return pendingWriteTasks
            .Concat(pendingReadTasks)
            .Concat(pendingVerifyTasks)
            .OrderBy(task => task.SequenceNumber)
            .ToArray();
    }

    public IReadOnlyList<TaskBase> GetPendingWriteTasks()
    {
        return pendingWriteTasks
            .OrderBy(task => task.SequenceNumber)
            .ToArray();
    }

    public IReadOnlyList<TaskBase> GetPendingReadTasks()
    {
        return pendingReadTasks
            .OrderBy(task => task.SequenceNumber)
            .ToArray();
    }

    public IReadOnlyList<TaskBase> GetPendingVerifyTasks()
    {
        return pendingVerifyTasks
            .OrderBy(task => task.SequenceNumber)
            .ToArray();
    }

    public void UpdateIndexByTask(LtfsIndex index, TaskBase task)
    {
        LtfsIndexOperations.ApplyTask(index, task);
    }

    public bool ReadFile(string fileName, LtfsFile file)
    {
        if (File.Exists(fileName))
            return false;

        try
        {
            using (FileStream fs = new(fileName, FileMode.CreateNew))
            {
                // empty file, create directly
                
                if (file.Length == 0)
                {
                    fs.Write([]);
                }
                else if (file.ExtentInfo?.Extent != null)
                {
                    foreach (var fe in file.ExtentInfo.Extent)
                    {
                        _tapeDrive.Locate(fe.StartBlock, PartitionToNumber(fe.Partition), LocateType.Block);
                        fs.Seek((long)fe.FileOffset, SeekOrigin.Begin);
                        ulong readBytes = 0;
                        while (readBytes < fe.ByteCount + fe.ByteOffset)
                        {
                                uint blocklen = (uint)Math.Min((ulong)LtfsLabelA!.Blocksize, fe.ByteCount + fe.ByteOffset - readBytes);
                            byte[]? data = null;
                            bool readsucc = false;
                            while (!readsucc)
                            {
                                data = _tapeDrive.ReadBlock(blocklen, true);
                                var sense = _tapeDrive.Sense;
                                if (((sense[2] >> 6) & 1) == 1)
                                {
                                    // end of file
                                    
                                    readsucc = true;
                                }
                                else if ((sense[2] & 0x0f) != 0)
                                {
                                    throw new Exception("SCSI sense error");
                                }
                                else
                                {
                                    readsucc = true;
                                }
                            }
                            if (data == null || data.Length != blocklen || blocklen == 0)
                                throw new IOException("block length mismatch or zero");
                            readBytes += blocklen - fe.ByteOffset;
                            fs.Write(data, (int)fe.ByteOffset, (int)(blocklen - fe.ByteOffset));
                        }
                    }
                }
            }

            // set file attributes
            var fi = new FileInfo(fileName)
            {
                CreationTimeUtc = file.CreationTime,
                LastWriteTimeUtc = file.ModifyTime,
                LastAccessTimeUtc = file.AccessTime,
                IsReadOnly = file.ReadOnly
            };
            return true;
        }
        catch
        {
            // can log error here
            return false;
        }
    }

    private LtfsIndex BuildPendingIndexView()
    {
        var index = (LtfsIndex)GetLatestIndex().Clone();
        foreach (var task in GetUncommittedTasks())
        {
            LtfsIndexOperations.ApplyTask(index, task);
        }

        return index;
    }

    private void EnqueueReplaceDelete(string normalizedTargetPath)
    {
        if (GetUncommittedTasks().Any(task => task is DeleteTask deleteTask && SamePath(deleteTask.TargetPath, normalizedTargetPath)))
            return;

        EnqueueTask(new DeleteTask
        {
            TargetPath = normalizedTargetPath,
        });
    }

    private void RemovePendingWriteTask(string normalizedTargetPath)
    {
        pendingWriteTasks.RemoveAll(task => IsUncommitted(task) && task is WriteTask writeTask && SamePath(writeTask.TargetPath, normalizedTargetPath));
    }

    private void RemovePendingTasks(string normalizedTargetPath, bool includeDescendants)
    {
        pendingWriteTasks.RemoveAll(task => IsUncommitted(task) && TaskTouchesPath(task, normalizedTargetPath, includeDescendants));
    }

    private bool TaskTouchesPath(TaskBase task, string normalizedTargetPath, bool includeDescendants)
    {
        return task switch
        {
            PathTaskBase pathTask => PathMatches(pathTask.TargetPath, normalizedTargetPath, includeDescendants),
            MoveTask moveTask => PathMatches(moveTask.SourcePath, normalizedTargetPath, includeDescendants)
                || PathMatches(moveTask.TargetPath, normalizedTargetPath, includeDescendants),
            _ => false,
        };
    }

    private static bool PathMatches(string taskPath, string normalizedTargetPath, bool includeDescendants)
    {
        if (SamePath(taskPath, normalizedTargetPath))
            return true;

        if (!includeDescendants)
            return false;

        var prefix = normalizedTargetPath == "/" ? "/" : normalizedTargetPath + "/";
        return LtfsIndexOperations.NormalizePath(taskPath).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SamePath(string left, string right)
    {
        return string.Equals(
            LtfsIndexOperations.NormalizePath(left),
            LtfsIndexOperations.NormalizePath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private void EnqueueTask(TaskBase task)
    {
        task.SequenceNumber = ++nextTaskSequence;
        GetTaskQueue(task).Add(task);
    }

    private void EnqueueReadTask(string sourcePath, string targetPath, LtfsFile sourceFile)
    {
        EnqueueTask(new ReadTask
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            SourceFile = sourceFile,
        });
    }

    private void EnqueueVerifyTask(string sourcePath, LtfsFile sourceFile)
    {
        EnqueueTask(new VerifyTask
        {
            SourcePath = sourcePath,
            SourceFile = sourceFile,
        });
    }

    private static IEnumerable<(string FilePath, LtfsFile File)> EnumerateDirectoryFiles(string directoryPath, LtfsDirectory directory)
    {
        foreach (var entry in directory.Contents)
        {
            switch (entry)
            {
                case LtfsFile file:
                    yield return (LtfsIndexOperations.NormalizePath(Path.Combine(directoryPath, file.Name.GetName())), file);
                    break;
                case LtfsDirectory subDirectory:
                    var subDirectoryPath = LtfsIndexOperations.NormalizePath(Path.Combine(directoryPath, subDirectory.Name.GetName()));
                    foreach (var item in EnumerateDirectoryFiles(subDirectoryPath, subDirectory))
                    {
                        yield return item;
                    }
                    break;
            }
        }
    }

    private List<TaskBase> GetTaskQueue(TaskBase task)
    {
        return task switch
        {
            ReadTask => pendingReadTasks,
            VerifyTask => pendingVerifyTasks,
            _ => pendingWriteTasks,
        };
    }

}
