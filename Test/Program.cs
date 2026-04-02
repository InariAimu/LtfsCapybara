
using System;
using System.Threading;
using Ltfs;
using TapeDrive;

Ltfs.Ltfs lt = new();

// Initialize the test console logger. Adjust level as desired.
Log.SetLogger(new ConsoleLogger { Level = LogLevel.Info });

Logger.Info("Loading LTFS from tape...");
lt.LoadTape();

var randomData = new byte[1048576]; // 1 MiB of random data
new Random().NextBytes(randomData);

var drive = lt.TapeDrive! as LTOTapeDrive;
drive.Locate(0, 0, locateType: TapeDrive.LocateType.EOD);
var pos = drive.ReadPosition();
Logger.Info($"Current tape position: {pos}");

// write 12 wraps of data, every 1 second, use TapeDrive.ReadErrorRate() to log the error rate
var dt = DateTime.Now;
var pi = LtoTape.CM.TapeInfo.GetPhysicInfo(5);
long wrapsize = pi.KBytesPerSet * 1024L * pi.SetsPerWrap;
long speed = 0;

for (long i = 0; i <= wrapsize * 12;)
{
    bool success = drive.BufferedWrite(randomData, 0x080000);

    i += randomData.Length;
    speed += randomData.Length;

    var now = DateTime.Now;
    var elapsed = now - dt;
    if (elapsed > TimeSpan.FromSeconds(1))
    {
        drive.ReadErrorRate(); // update error rate
        dt = now; // reset the timer
        Logger.Info($"+{speed / elapsed.TotalSeconds / 1024 / 1024:F2} MB: {drive.GetReadableChannelErrorRates()}");
        speed = 0; // reset speed counter
    }
}

Console.ReadKey();
