
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

//lt.AddFile(srcFile, ltfsFile);

//lt.AddDirectory("\\\\NekoHouse\\video\\Anime\\葬送的芙莉莲.Sousou.no.Frieren.S01.2023.1080p.CR.WEB-DL.H264.AAC-RLWeb", "/Anime/葬送的芙莉莲.Sousou.no.Frieren.S01.2023.1080p.CR.WEB-DL.H264.AAC-RLWeb");

//\\NekoHouse\video\Anime\[ANK-Raws] CANAAN (BDrip 1920x1080 x264 FLAC Hi10P ver)

string folder = "[Skytree][名侦探柯南][Detective_Conan][638-720][GB_JP][X264_AAC][720P]";
//lt.AddDirectory("\\\\NekoHouse\\video\\Anime\\" + folder, "/Anime/" + folder);

lt.AddDirectory("\\\\NekoHouse\\video\\Anime", "/Anime");


Logger.Info("Writing files to tape...");
await lt.PerformWriteTasks();

Logger.Info("Writing LTFS Index...");
lt.WriteLtfsIndex();

Logger.Info("Done.");
lt.TapeDrive?.Dispose();

Console.ReadKey();
