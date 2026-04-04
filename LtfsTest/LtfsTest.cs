using System;
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

}

