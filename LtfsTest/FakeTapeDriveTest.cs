using Ltfs;

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
}