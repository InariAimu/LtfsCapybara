using Ltfs;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

using TapeDrive;

namespace LtfsTest;

public class FakeTapeDriveTest
{
    [Fact]
    public void FormatThenReadLtfs_RoundTripsMetadata()
    {
        var drive = new FakeTapeDrive();

        var writer = new Ltfs.Ltfs();
        writer.SetTapeDrive(drive);

        var formatResult = writer.Format(new FormatParam
        {
            Barcode = "TEST001L6",
            VolumeName = "Fake Drive Volume",
        });

        Assert.True(formatResult);

        var reader = new Ltfs.Ltfs();
        reader.SetTapeDrive(drive);

        Assert.True(reader.ReadLtfs());
        Assert.Equal("TEST001L6", reader.Barcode);
        Assert.Equal("Fake Drive Volume", reader.LtfsIndexCurr?.Directory?.Name?.Value);
    }

    [Fact]
    public void TryReadPerformanceData_WhenIoIsBusy_ReturnsImmediately()
    {
        using var drive = new LTOTapeDrive(open: false);
        var ioSyncField = typeof(LTOTapeDrive).GetField("_ioSync", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(ioSyncField);

        var ioSync = ioSyncField!.GetValue(drive);
        Assert.NotNull(ioSync);

        Monitor.Enter(ioSync!);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = drive.TryReadPerformanceData(out var performanceData);
            stopwatch.Stop();

            Assert.False(result);
            Assert.Null(performanceData);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromMilliseconds(200),
                $"Expected non-blocking performance sampling when IO is busy, elapsed {stopwatch.Elapsed}.");
        }
        finally
        {
            Monitor.Exit(ioSync!);
        }
    }
}