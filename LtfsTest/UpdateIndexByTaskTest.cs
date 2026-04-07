using System;
using System.Collections.Generic;
using Xunit;
using Ltfs;
using Ltfs.Index;
using Ltfs.Tasks;
using Ltfs.Label;
using TapeDrive;

namespace LtfsTest
{
    public class UpdateIndexByTaskTest
    {
        [Fact]
        public void WriteReplaceDelete_UpdateIndexBehavesAsExpected()
        {
            // arrange
            var ltfs = new Ltfs.Ltfs();
            var index = LtfsIndex.Default();
            index.Directory = LtfsDirectory.Default();
            index.Directory.Name = new NameType { Value = "VOLTEST" };
            index.HighestFileUID = 1;

            // prepare a write task for path /dir1/file1.txt
            var file = LtfsFile.Default();
            file.Name = "file1.txt";
            file.Length = 123;

            var writeTask = new WriteTask
            {
                LocalPath = "C:\\tmp\\file1.txt",
                TargetPath = "/dir1/file1.txt",
                LtfsTargetPath = file
            };

            // act - write
            ltfs.UpdateIndexByTask(index, writeTask);

            // assert - file exists under dir1
            var dir1 = index.Directory["dir1"] as LtfsDirectory;
            Assert.NotNull(dir1);
            var createdFile = dir1!["file1.txt"] as LtfsFile;
            Assert.NotNull(createdFile);
            Assert.Equal((ulong)123, createdFile!.Length);
            Assert.True(index.HighestFileUID >= createdFile.FileUID);

            // act - replace
            var newFile = LtfsFile.Default();
            newFile.Name = "file1.txt";
            newFile.Length = 456;
            var replaceTask = new WriteTask
            {
                LocalPath = "C:\\tmp\\file1_v2.txt",
                TargetPath = "/dir1/file1.txt",
                LtfsTargetPath = newFile
            };
            ltfs.UpdateIndexByTask(index, replaceTask);

            var replaced = dir1["file1.txt"] as LtfsFile;
            Assert.NotNull(replaced);
            Assert.Equal((ulong)456, replaced!.Length);

            // act - delete
            var deleteTask = new DeleteTask
            {
                TargetPath = "/dir1/file1.txt"
            };
            ltfs.UpdateIndexByTask(index, deleteTask);

            var afterDelete = dir1["file1.txt"] as LtfsFile;
            Assert.Null(afterDelete);
        }

        [Fact]
        public void PendingIndexView_AssignsUniqueFileUidsAcrossNestedNodes()
        {
            var ltfs = new Ltfs.Ltfs();
            ltfs.LtfsDataTempIndexs.Clear();
            ltfs.LtfsDataTempIndexs.Add(LtfsIndex.Default());

            var file1 = Path.GetTempFileName();
            var file2 = Path.GetTempFileName();
            var file3 = Path.GetTempFileName();

            try
            {
                File.WriteAllText(file1, "one");
                File.WriteAllText(file2, "two");
                File.WriteAllText(file3, "three");

                ltfs.AddFile(file1, "/parent/a.txt");
                ltfs.AddFile(file2, "/parent/b.txt");
                ltfs.AddFile(file3, "/parent/child/c.txt");

                var parent = Assert.IsType<LtfsDirectory>(ltfs.FindDirectory("/parent"));
                var child = Assert.IsType<LtfsDirectory>(ltfs.FindDirectory("/parent/child"));
                var fileNode1 = Assert.IsType<LtfsFile>(ltfs.FindFile("/parent/a.txt"));
                var fileNode2 = Assert.IsType<LtfsFile>(ltfs.FindFile("/parent/b.txt"));
                var fileNode3 = Assert.IsType<LtfsFile>(ltfs.FindFile("/parent/child/c.txt"));

                var uids = new HashSet<ulong>
                {
                    parent.FileUID,
                    child.FileUID,
                    fileNode1.FileUID,
                    fileNode2.FileUID,
                    fileNode3.FileUID,
                };

                Assert.Equal(5, uids.Count);
            }
            finally
            {
                File.Delete(file1);
                File.Delete(file2);
                File.Delete(file3);
            }
        }

        [Fact]
        public void PendingIndexView_AfterReadLtfsState_AssignsUniqueFileUidsAcrossNewNodes()
        {
            var ltfs = CreateLtfsAfterReadLtfsState();

            var file1 = Path.GetTempFileName();
            var file2 = Path.GetTempFileName();
            var file3 = Path.GetTempFileName();

            try
            {
                File.WriteAllText(file1, "one");
                File.WriteAllText(file2, "two");
                File.WriteAllText(file3, "three");

                ltfs.AddFile(file1, "/loaded/parent/a.txt");
                ltfs.AddFile(file2, "/loaded/parent/b.txt");
                ltfs.AddFile(file3, "/loaded/parent/child/c.txt");

                var parent = Assert.IsType<LtfsDirectory>(ltfs.FindDirectory("/loaded/parent"));
                var child = Assert.IsType<LtfsDirectory>(ltfs.FindDirectory("/loaded/parent/child"));
                var fileNode1 = Assert.IsType<LtfsFile>(ltfs.FindFile("/loaded/parent/a.txt"));
                var fileNode2 = Assert.IsType<LtfsFile>(ltfs.FindFile("/loaded/parent/b.txt"));
                var fileNode3 = Assert.IsType<LtfsFile>(ltfs.FindFile("/loaded/parent/child/c.txt"));

                var uids = new HashSet<ulong>
                {
                    parent.FileUID,
                    child.FileUID,
                    fileNode1.FileUID,
                    fileNode2.FileUID,
                    fileNode3.FileUID,
                };

                Assert.Equal(5, uids.Count);
                Assert.True(fileNode3.FileUID > 10);
            }
            finally
            {
                File.Delete(file1);
                File.Delete(file2);
                File.Delete(file3);
            }
        }

        [Fact]
        public void WriteIndexToDataPartition_KeepsWorkingIndexCurrentForSubsequentMetadataUpdates()
        {
            var ltfs = new Ltfs.Ltfs();
            ltfs.SetTapeDrive(new FakeTapeDrive());
            ltfs.LtfsDataTempIndexs.Clear();
            var workingIndex = LtfsIndex.Default();
            ltfs.LtfsDataTempIndexs.Add(workingIndex);
            ltfs.LtfsIndexCurr = workingIndex;
            ltfs.LtfsLabelA = new LtfsLabel
            {
                Version = "2.4.0",
                Creator = "test",
                Formattime = DateTime.UtcNow,
                Volumeuuid = Guid.NewGuid(),
                Location = new Location { Partitions = { "a" } },
                Partitions = new Partitions { Index = "a", Data = "b" },
                Blocksize = 4,
                Compression = true,
            };

            var firstTask = CreateWrittenFileTask("/first.bin", 1, "AAAABBBB");
            ltfs.UpdateIndexByTask(workingIndex, firstTask);

            ltfs.WriteIndexToDataPartition();

            var secondTask = CreateWrittenFileTask("/second.bin", 3, "CCCCDDDD");
            ltfs.UpdateIndexByTask(workingIndex, secondTask);

            var firstFile = Assert.IsType<LtfsFile>(ltfs.LtfsIndexCurr!.Directory["first.bin"]);
            var secondFile = Assert.IsType<LtfsFile>(ltfs.LtfsIndexCurr!.Directory["second.bin"]);

            Assert.NotNull(firstFile.ExtentInfo);
            Assert.NotNull(secondFile.ExtentInfo);
            Assert.Equal(secondTask.LtfsTargetPath.ExtendedAttributes!["ltfs.hash.crc64sum"], secondFile.ExtendedAttributes!["ltfs.hash.crc64sum"]);
        }

        [Fact]
        public void WriteIndexToDataPartition_AfterReadLtfsState_KeepsWorkingIndexCurrentForSubsequentMetadataUpdates()
        {
            var ltfs = CreateLtfsAfterReadLtfsState();
            ltfs.SetTapeDrive(new FakeTapeDrive());

            var firstTask = CreateWrittenFileTask("/loaded/first.bin", 11, "AAAABBBB");
            ltfs.UpdateIndexByTask(ltfs.LtfsIndexCurr!, firstTask);

            ltfs.WriteIndexToDataPartition();

            var secondTask = CreateWrittenFileTask("/loaded/second.bin", 13, "CCCCDDDD");
            ltfs.UpdateIndexByTask(ltfs.LtfsIndexCurr!, secondTask);

            var loadedDirectory = Assert.IsType<LtfsDirectory>(ltfs.LtfsIndexCurr!.Directory["loaded"]);
            var firstFile = Assert.IsType<LtfsFile>(loadedDirectory["first.bin"]);
            var secondFile = Assert.IsType<LtfsFile>(loadedDirectory["second.bin"]);

            Assert.NotNull(firstFile.ExtentInfo);
            Assert.NotNull(secondFile.ExtentInfo);
            Assert.Equal(firstTask.LtfsTargetPath.ExtendedAttributes!["ltfs.hash.crc64sum"], firstFile.ExtendedAttributes!["ltfs.hash.crc64sum"]);
            Assert.Equal(secondTask.LtfsTargetPath.ExtendedAttributes!["ltfs.hash.crc64sum"], secondFile.ExtendedAttributes!["ltfs.hash.crc64sum"]);
        }

        private static Ltfs.Ltfs CreateLtfsAfterReadLtfsState()
        {
            var ltfs = new Ltfs.Ltfs();
            ltfs.LtfsDataTempIndexs.Clear();

            var latestIndex = LtfsIndex.Default();
            latestIndex.GenerationNumber = 7;
            latestIndex.HighestFileUID = 10;
            latestIndex.Location = new TapePosition
            {
                Partition = "a",
                StartBlock = 100,
            };
            latestIndex.PreviousGenerationLocation = new TapePosition
            {
                Partition = "b",
                StartBlock = 80,
            };

            var rootDir = LtfsDirectory.Default();
            rootDir.Name = "loaded";
            rootDir.FileUID = 10;
            latestIndex.Directory["loaded"] = rootDir;

            var dataIndex = (LtfsIndex)latestIndex.Clone();
            dataIndex.Location = new TapePosition
            {
                Partition = latestIndex.PreviousGenerationLocation!.Partition,
                StartBlock = latestIndex.PreviousGenerationLocation.StartBlock,
            };

            ltfs.LtfsIndexA = latestIndex;
            ltfs.LtfsIndexB = dataIndex;
            ltfs.LtfsIndexCurr = (LtfsIndex)latestIndex.Clone();
            ltfs.LtfsDataTempIndexs.Add(dataIndex);
            ltfs.LtfsLabelA = new LtfsLabel
            {
                Version = "2.4.0",
                Creator = "test",
                Formattime = DateTime.UtcNow,
                Volumeuuid = Guid.NewGuid(),
                Location = new Location { Partitions = { "a" } },
                Partitions = new Partitions { Index = "a", Data = "b" },
                Blocksize = 4,
                Compression = true,
            };

            return ltfs;
        }

        private static WriteTask CreateWrittenFileTask(string targetPath, ulong startBlock, string content)
        {
            var data = System.Text.Encoding.ASCII.GetBytes(content);
            var file = LtfsFile.Default();
            file.Name = Path.GetFileName(targetPath);
            file.Length = (ulong)data.Length;
            file.ExtentInfo = new ExtentInfo
            {
                Extent = [
                    new Extent
                    {
                        Partition = "b",
                        StartBlock = startBlock,
                        FileOffset = 0,
                        ByteOffset = 0,
                        ByteCount = (ulong)data.Length,
                    }
                ]
            };
            file.ExtendedAttributes = new ExtendedAttributes
            {
                Xattrs = [
                    new XAttr("ltfs.hash.crc64sum", ComputeCrc64(data)),
                ]
            };

            return new WriteTask
            {
                LocalPath = targetPath,
                TargetPath = targetPath,
                LtfsTargetPath = file,
            };
        }

        private static string ComputeCrc64(byte[] data)
        {
            var crc64 = new System.IO.Hashing.Crc64();
            crc64.Append(data);
            return crc64.GetCurrentHashAsUInt64().ToString("X16");
        }
    }
}
