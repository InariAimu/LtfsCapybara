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

        var index = ltfs.GetLatestIndex();
        var file = LtfsFile.Default();

        file.Name = "test.txt";

        ltfs.CreateFile(index.Directory, file);

        Assert.True(index.Directory.Contents.Length == 1);
        Assert.True(((LtfsFile)index.Directory.Contents[0]).Name.Value == "test.txt");
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

}

