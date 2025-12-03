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

public class LtfsDirectoryTest
{
    /// <summary>
    /// Tests the <see cref="LtfsDirectory.Indexer"/> property to find a node by name.
    /// </summary>
    /// <remarks>
    /// The test creates a directory tree /dir1/dir2/file.txt and tests that the indexer can find the file and the dir2.
    /// </remarks>
    [Fact]
    public void DirectoryIndexer()
    {
        // Create dir structure /dir1/dir2/file.txt
        var file = LtfsFile.Default();
        file.Name = "file.txt";

        var dir2 = LtfsDirectory.Default();
        dir2.Name = "dir2";
        dir2.Contents = [file];

        var dir1 = LtfsDirectory.Default();
        dir1.Name = "dir1";
        dir1.Contents = [dir2];

        var root = LtfsDirectory.Default();
        root.Name = "";
        root.Contents = [dir1];

        // Test indexer lookup
        var foundFile = dir2["file.txt"] as LtfsFile;
        Assert.NotNull(foundFile);
        Assert.Equal("file.txt", foundFile.Name.GetName());

        var foundDir1 = root["dir1"] as LtfsDirectory;
        Assert.NotNull(foundDir1);
        Assert.Equal("dir1", foundDir1.Name.GetName());
    }


    [Fact]
    public void AddDirectoryIndexer()
    {
        var root = LtfsDirectory.Default();
        root.Name = "";

        var dir1 = LtfsDirectory.Default();
        dir1.Name = "dir1";

        root["dir1"] = dir1;

        var fdir1 = root["dir1"] as LtfsDirectory;
        Assert.NotNull(fdir1);
        Assert.Equal("dir1", fdir1.Name.GetName());
    }
}
