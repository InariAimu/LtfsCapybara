using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Reflection.Emit;
using System.Xml.Schema;
using System.Xml;
using System.Xml.Serialization;
using Xunit.Abstractions;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Ltfs.Label;
using Ltfs;
using Ltfs.Index;
using Microsoft.Extensions.Logging.Abstractions;
using LtfsServer.Features.LocalTapes;
using Ltfs.Tasks;

namespace LtfsTest;

public class LtfsTest
{
    [Fact]
    public void Vol1Test()
    {
        var vol1 = new Vol1Label("TEST01L6", "");
        try
        {
            Vol1Label.ToByteArray(vol1);
        }
        catch (Exception ex)
        {
            Assert.Fail(ex.Message);
        }
    }


    [Fact]
    public void FindFile()
    {
        Ltfs.Ltfs ltfs = new Ltfs.Ltfs();
        ltfs.LtfsDataTempIndexs.Clear();
        ltfs.LtfsDataTempIndexs.Add(LtfsIndex.Default());

        var index = ltfs.GetLatestIndex();

        var dir1 = LtfsDirectory.Default();
        dir1.Name = "dir1";
        index.Root["dir1"] = dir1;

        var file1 = LtfsFile.Default();
        file1.Name = "file1.txt";
        dir1["file1.txt"] = file1;

        var findFile1 = ltfs.FindFile("/dir1/file1.txt") as LtfsFile;

        Assert.NotNull(findFile1);
        Assert.True(findFile1.Name.Value == "file1.txt");

    }


    [Fact]
    public void AddFile()
    {
        Ltfs.Ltfs ltfs = new Ltfs.Ltfs();
        ltfs.LtfsDataTempIndexs.Clear();
        ltfs.LtfsDataTempIndexs.Add(LtfsIndex.Default());

        ltfs.CreateDirectory("/dir1");

        var dir = ltfs.FindDirectory("/dir1");

        Assert.NotNull(dir);
        Assert.Equal("dir1", dir!.Name.Value);
    }


    [Fact]
    public void AddFile_ExistingFile_QueuesDeleteThenWrite()
    {
        Ltfs.Ltfs ltfs = new Ltfs.Ltfs();
        ltfs.LtfsDataTempIndexs.Clear();
        ltfs.LtfsDataTempIndexs.Add(LtfsIndex.Default());

        var index = ltfs.GetLatestIndex();
        var original = LtfsFile.Default();
        original.Name = "file1.txt";
        index.Directory["file1.txt"] = original;

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "replacement");

            ltfs.AddFile(tempFile, "/file1.txt");

            var tasks = ltfs.GetPendingTasks();
            Assert.Equal(2, tasks.Count);
            Assert.IsType<DeleteTask>(tasks[0]);
            Assert.IsType<WriteTask>(tasks[1]);
            Assert.Equal("/file1.txt", ((DeleteTask)tasks[0]).TargetPath);
            Assert.Equal("/file1.txt", ((WriteTask)tasks[1]).TargetPath);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }


    [Fact]
    public void DeletePath_RemovesPendingWriteForNewFile()
    {
        Ltfs.Ltfs ltfs = new Ltfs.Ltfs();
        ltfs.LtfsDataTempIndexs.Clear();
        ltfs.LtfsDataTempIndexs.Add(LtfsIndex.Default());

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "new file");

            ltfs.AddFile(tempFile, "/new-file.txt");
            ltfs.DeletePath("/new-file.txt");

            Assert.Empty(ltfs.GetPendingTasks());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }


    [Fact]
    public void RemoveFile()
    {

    }


    [Fact]
    public void RemoveAll()
    {

    }


    [Fact]
    public void AddDirectory()
    {

    }


    [Fact]
    public async Task LocalTapeRegistry_FindsLatestCmEntry()
    {
    }

    [Fact]
    public async Task PrefetchTask_SkipsFilesBelowDirectWriteThreshold()
    {
        var ltfs = new Ltfs.Ltfs
        {
            SmallFileDirectWriteThresholdBytes = 4 * 1024,
            SmallFilePrefetchThresholdBytes = 64 * 1024,
        };

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, new byte[1024]);

            var ltfsFile = LtfsFile.Default();
            ltfsFile.Name = "tiny.bin";
            ltfsFile.Length = 1024;

            var writeTask = new WriteTask
            {
                LocalPath = tempFile,
                TargetPath = "/tiny.bin",
                LtfsTargetPath = ltfsFile,
            };

            using var prefetchSemaphore = new SemaphoreSlim(1);
            using var prefetchSignal = new SemaphoreSlim(0);

            await InvokePrefetchTaskAsync(ltfs, [writeTask], prefetchSemaphore, prefetchSignal, 8, ltfs.SmallFilePrefetchThresholdBytes, ltfs.SmallFileDirectWriteThresholdBytes);

            var reader = GetBufferedReader(ltfs, tempFile);
            Assert.Null(reader);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PrefetchTask_BuffersFilesAboveDirectWriteThreshold()
    {
        var ltfs = new Ltfs.Ltfs
        {
            SmallFileDirectWriteThresholdBytes = 4 * 1024,
            SmallFilePrefetchThresholdBytes = 64 * 1024,
        };

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, new byte[8 * 1024]);

            var ltfsFile = LtfsFile.Default();
            ltfsFile.Name = "prefetch.bin";
            ltfsFile.Length = 8 * 1024;

            var writeTask = new WriteTask
            {
                LocalPath = tempFile,
                TargetPath = "/prefetch.bin",
                LtfsTargetPath = ltfsFile,
            };

            using var prefetchSemaphore = new SemaphoreSlim(1);
            using var prefetchSignal = new SemaphoreSlim(0);

            await InvokePrefetchTaskAsync(ltfs, [writeTask], prefetchSemaphore, prefetchSignal, 8, ltfs.SmallFilePrefetchThresholdBytes, ltfs.SmallFileDirectWriteThresholdBytes);

            var reader = GetBufferedReader(ltfs, tempFile);
            Assert.NotNull(reader);

            await RemoveBufferedFileAsync(ltfs, tempFile);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void BuildHashXattrs_UsesCrc64WhenConfigured()
    {
        var xattrs = BuildHashXattrs(FileChecksumAlgorithm.Crc64, "abc"u8.ToArray());

        Assert.Single(xattrs);
        Assert.Equal("ltfs.hash.crc64sum", xattrs[0].Key.Value);
        Assert.Equal("66501A349A0E0855", xattrs[0].Value.Value);
    }

    [Fact]
    public void BuildHashXattrs_UsesSha1WhenConfigured()
    {
        var xattrs = BuildHashXattrs(FileChecksumAlgorithm.Sha1, "abc"u8.ToArray());

        Assert.Single(xattrs);
        Assert.Equal("ltfs.hash.sha1sum", xattrs[0].Key.Value);
        Assert.Equal(Convert.ToHexString(SHA1.HashData("abc"u8.ToArray())), xattrs[0].Value.Value);
    }

    [Fact]
    public void BuildHashXattrs_UsesBothHashesWhenConfigured()
    {
        var xattrs = BuildHashXattrs(FileChecksumAlgorithm.Crc64AndSha1, "abc"u8.ToArray());

        Assert.Equal(2, xattrs.Length);
        Assert.Equal("ltfs.hash.crc64sum", xattrs[0].Key.Value);
        Assert.Equal("66501A349A0E0855", xattrs[0].Value.Value);
        Assert.Equal("ltfs.hash.sha1sum", xattrs[1].Key.Value);
        Assert.Equal(Convert.ToHexString(SHA1.HashData("abc"u8.ToArray())), xattrs[1].Value.Value);
    }

    private static async Task InvokePrefetchTaskAsync(Ltfs.Ltfs ltfs, IReadOnlyList<WriteTask> writeTasks, SemaphoreSlim prefetchSemaphore, SemaphoreSlim prefetchSignal, int prefetchWindow, int prefetchThreshold, int directWriteThreshold)
    {
        var method = typeof(Ltfs.Ltfs).GetMethod("PrefetchTask", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PrefetchTask method was not found.");

        var task = (Task?)method.Invoke(ltfs, new object?[] { writeTasks, prefetchSemaphore, prefetchSignal, prefetchWindow, prefetchThreshold, directWriteThreshold })
            ?? throw new InvalidOperationException("PrefetchTask did not return a task.");

        await task;
    }

    private static System.Threading.Channels.ChannelReader<FileBuffer.SmallFileBufferItem>? GetBufferedReader(Ltfs.Ltfs ltfs, string path)
    {
        var field = typeof(Ltfs.Ltfs).GetField("fileBuffer", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("fileBuffer field was not found.");

        var buffer = (FileBuffer?)field.GetValue(ltfs)
            ?? throw new InvalidOperationException("fileBuffer was not initialized.");

        return buffer.GetReader(path);
    }

    private static async Task RemoveBufferedFileAsync(Ltfs.Ltfs ltfs, string path)
    {
        var field = typeof(Ltfs.Ltfs).GetField("fileBuffer", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("fileBuffer field was not found.");

        var buffer = (FileBuffer?)field.GetValue(ltfs)
            ?? throw new InvalidOperationException("fileBuffer was not initialized.");

        await buffer.RemoveAsync(path);
    }

    private static XAttr[] BuildHashXattrs(FileChecksumAlgorithm algorithm, byte[] data)
    {
        var ltfs = new Ltfs.Ltfs
        {
            ChecksumAlgorithm = algorithm,
        };

        var resetMethod = typeof(Ltfs.Ltfs).GetMethod("ResetHashes", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ResetHashes method was not found.");
        var appendMethod = typeof(Ltfs.Ltfs).GetMethod("AppendHashBytes", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("AppendHashBytes method was not found.");
        var buildMethod = typeof(Ltfs.Ltfs).GetMethod("BuildHashXattrs", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildHashXattrs method was not found.");
        var disposeMethod = typeof(Ltfs.Ltfs).GetMethod("DisposeHashes", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DisposeHashes method was not found.");

        resetMethod.Invoke(ltfs, null);
        try
        {
            appendMethod.Invoke(ltfs, new object?[] { data });
            return (XAttr[]?)buildMethod.Invoke(ltfs, null)
                ?? throw new InvalidOperationException("BuildHashXattrs returned null.");
        }
        finally
        {
            disposeMethod.Invoke(ltfs, null);
        }
    }

}

