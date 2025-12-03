using Ltfs.Index;

using TapeDrive;

namespace Ltfs;

public partial class Ltfs
{
    protected List<FileTask> fileTasks = new();

    public void AddFile(LtfsDirectory directory, LtfsFile file)
    {
        // check for existing file with same name in the directory
        bool exists = directory.Contents
            .OfType<LtfsFile>()
            .Any(f => f.Name.GetName() == file.Name.GetName());
        if (exists)
        {
            // can choose to throw an exception or simply return
            
            throw new InvalidOperationException($"File with the same name already exists in the directory: {file.Name.GetName()}");
            // return; // Uncomment this line if you prefer not to throw an exception
        }
        directory.Contents = directory.Contents.Append(file).ToArray();
    }

    public LtfsFile? FindFile(string path)
    {
        var index = GetLatestIndex();
        if (string.IsNullOrEmpty(path) || index?.Directory == null)
            return null;

        // Remove leading slash and split path
        // 
        var parts = path.Trim('/').Split('/');

        LtfsFile? FindInDir(object dirObj, string[] segments, int depth)
        {
            if (dirObj is LtfsDirectory dir)
            {
                if (depth == segments.Length - 1)
                {
                    // last level, find file
                    foreach (var item in dir.Contents)
                    {
                        if (item is LtfsFile file && file.Name.GetName() == segments[depth])
                            return file;
                    }
                    return null;
                }
                else
                {
                    // find next level directory
                    foreach (var item in dir.Contents)
                    {
                        if (item is LtfsDirectory subDir && subDir.Name.GetName() == segments[depth])
                        {
                            var found = FindInDir(subDir, segments, depth + 1);
                            if (found != null)
                                return found;
                        }
                    }
                    return null;
                }
            }
            return null;
        }

        return FindInDir(index.Directory, parts, 0);
    }

    public void AddFileToLtfs(string fileName, string targetPath)
    {
        if (!File.Exists(fileName))
            return;

        FileInfo fileInfo = new FileInfo(fileName);

        var ltfsFile = LtfsFile.Default();
        ltfsFile.Name = Path.GetFileName(targetPath);
        ltfsFile.Length = (ulong)fileInfo.Length;
        ltfsFile.CreationTime = fileInfo.CreationTimeUtc;
        ltfsFile.ModifyTime = fileInfo.LastWriteTimeUtc;
        ltfsFile.AccessTime = fileInfo.LastAccessTimeUtc;
        ltfsFile.ReadOnly = fileInfo.IsReadOnly;
        ltfsFile.FileUID = 0; // will be assigned when updating index

        FileTask task = new FileTask
        {
            TaskType = FileTaskType.Write,
            LocalPath = fileName,
            TargetPath = targetPath,
            LtfsPath = ltfsFile
        };

        if (FindFile(targetPath) != null)
        {
            task.TaskType = FileTaskType.Replace;
        }

        fileTasks.Add(task);
    }

    public void UpdateIndexByTask(LtfsIndex index, FileTask task)
    {
        // when a task is finished, update the ltfs index accordingly
        if (index == null || task == null)
            return;
        
        // normalize and split path
        var parts = task.TargetPath?.Trim('/')?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

        if (parts.Length == 0)
            return;

        // traverse/create directories to reach parent directory
        LtfsDirectory current = index.Directory!;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var name = parts[i];
            var obj = current[name];
            if (obj is LtfsDirectory dir)
            {
                current = dir;
            }
            else
            {
                // create missing directory
                var newDir = new LtfsDirectory
                {
                    Name = new NameType { Value = name },
                    FileUID = (uint)(index.HighestFileUID + 1),
                    CreationTime = DateTime.UtcNow,
                    ChangeTime = DateTime.UtcNow,
                    ModifyTime = DateTime.UtcNow,
                    AccessTime = DateTime.UtcNow,
                    BackupTime = DateTime.UtcNow,
                    ReadOnly = false,
                    Contents = Array.Empty<object>(),
                };
                // bump highest uid for directory
                index.HighestFileUID += 1;
                current[name] = newDir;
                current = newDir;
            }
        }

        string fileName = parts[^1];

        switch (task.TaskType)
        {
            case FileTaskType.Write:
                // add new file (do not clobber existing)
                if (current[fileName] is not LtfsFile)
                {
                    // ensure the LtfsFile has a valid FileUID
                    if (task.LtfsPath.FileUID == 0)
                    {
                        task.LtfsPath.FileUID = index.HighestFileUID + 1;
                        index.HighestFileUID += 1;
                    }
                    current[fileName] = task.LtfsPath;
                }
                else
                {
                    // file exists; treat like replace
                    current[fileName] = task.LtfsPath;
                }
                break;

            case FileTaskType.Replace:
                // replace existing file or add if missing
                if (task.LtfsPath.FileUID == 0)
                {
                    task.LtfsPath.FileUID = index.HighestFileUID + 1;
                    index.HighestFileUID += 1;
                }
                current[fileName] = task.LtfsPath;
                break;

            case FileTaskType.Delete:
                // remove matching file entry from directory
                var remaining = current.Contents.Where(o => !(o is LtfsFile f && f.Name.GetName() == fileName)).ToArray();
                current.Contents = remaining;
                break;
        }

        // update directory and index timestamps
        current.ModifyTime = DateTime.UtcNow;
        current.ChangeTime = DateTime.UtcNow;
        index.UpdateTime = DateTime.UtcNow;
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
                            uint blocklen = (uint)Math.Min((ulong)LtfsLabelA.Blocksize, fe.ByteCount + fe.ByteOffset - readBytes);
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

}
