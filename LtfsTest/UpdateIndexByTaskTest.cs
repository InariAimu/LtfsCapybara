using System;
using Xunit;
using Ltfs;
using Ltfs.Index;
using Ltfs.Tasks;

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
    }
}
