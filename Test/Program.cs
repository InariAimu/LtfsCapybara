
using Ltfs;

Ltfs.Ltfs lt = new();

// Initialize the test console logger. Adjust level as desired.
Log.SetLogger(new ConsoleLogger { Level = LogLevel.Info });

Logger.Info("Loading LTFS from tape...");
lt.LoadTape();

//lt.Format(new LtfsFormatParam()
//{
//    Barcode = "G00124L6",
//    VolumeName = "TESTVOL",
//    BlockSize = 524288,
//});

Logger.Info("Reading LTFS Index...");
lt.ReadLtfs();

var srcFile = "";
var ltfsFile = "";

lt.AddFile(srcFile, ltfsFile);

Logger.Info("Writing files to tape...");
await lt.PerformWriteTasks();

Logger.Info("Writing LTFS Index...");
lt.WriteLtfsIndex();

Logger.Info("Done.");
lt.TapeDrive?.Dispose();

Console.ReadKey();
