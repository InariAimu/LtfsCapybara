
Ltfs.Ltfs lt = new();

Console.WriteLine("Loading LTFS from tape...");
lt.LoadTape();

//lt.Format(new LtfsFormatParam()
//{
//    Barcode = "G00124L6",
//    VolumeName = "TESTVOL",
//    BlockSize = 524288,
//});

Console.WriteLine("Reading LTFS Index...");
lt.ReadLtfs();

var srcFile = "";
var ltfsFile = "";

lt.AddFileToLtfs(srcFile, ltfsFile);

Console.WriteLine("Writing files to tape...");
lt.WriteAll();

Console.WriteLine("Writing LTFS Index...");
lt.WriteLtfsIndex();

Console.WriteLine("Done.");
lt.TapeDrive?.Dispose();

Console.ReadKey();
