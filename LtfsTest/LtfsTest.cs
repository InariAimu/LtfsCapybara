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
using LtfsServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

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


    [Fact]
    public async Task LocalTapeRegistry_FindsLatestCmEntry()
    {
        var root = Path.Combine(Path.GetTempPath(), nameof(LocalTapeRegistry_FindsLatestCmEntry), Guid.NewGuid().ToString("N"));
        var tapeName = "C00392L6";
        var tapeDir = Path.Combine(root, tapeName);
        Directory.CreateDirectory(tapeDir);

        try
        {
            var xmlPath = Path.Combine(tapeDir, "C00392L6_P0_G3_L105310_20250830_171308.6964061.xml");
            await File.WriteAllTextAsync(xmlPath, "<ltfsindex />");

            var cmSource = Path.Combine(AppContext.BaseDirectory, "LTFSIndex_Autosave_C00392L6_GEN3_Pb_B105310_20250830_171308.6964061.cm");
            var cmTarget = Path.Combine(tapeDir, "Capture_G3_20250830_171308.6964061.cm");
            File.Copy(cmSource, cmTarget);

            var registry = new LocalTapeRegistry(NullLogger<LocalTapeRegistry>.Instance);
            await registry.InitializeAsync(root);

            var files = registry.GetFiles(tapeName).OrderByDescending(f => f.Index.Ticks).ToArray();

            Assert.Equal(2, files.Length);

            var cmInfo = Assert.Single(files.Where(f => f.Index.FileName.EndsWith(".cm", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal("Capture_G3_20250830_171308.6964061.cm", cmInfo.Index.FileName);
            Assert.Equal(3, cmInfo.Index.Generation);

            var summary = Assert.Single(registry.GetTapeSummaries().Where(s => s.TapeName == tapeName));
            Assert.True(summary.Generation > 0);
            Assert.NotEmpty(summary.ParticleType);
            Assert.NotEmpty(summary.Vendor);
            Assert.True(summary.TotalSizeBytes > 0);
            Assert.True(summary.FreeSizeBytes >= 0);

            var xmlInfo = Assert.Single(files.Where(f => f.Index.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(0, xmlInfo.Index.Partition);
            Assert.Equal(105310, xmlInfo.Index.LocationStartBlock);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

}

