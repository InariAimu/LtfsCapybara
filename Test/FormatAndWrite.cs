
using System;
using System.Threading.Tasks;
using Ltfs;
using TapeDrive;

namespace Test;

public class FormatAndWrite
{
    public static async Task Test(Ltfs.Ltfs lt)
    {
        Logger.Info("Loading LTFS from tape...");
        lt.LoadTape();

        var barcode = "";

        var needFormat = false;

        if (needFormat)
        {
            Logger.Info("Formatting tape...");
            lt.Format(new FormatParam()
            {
                Barcode = barcode,
                VolumeName = barcode,
                BlockSize = 524288,
            });
        }

        Logger.Info("Reading LTFS Index...");
        lt.ReadLtfs();

        {
            string folder = "";
            lt.AddDirectory("\\" + folder, "/" + folder);
        }

        {
            string file = "";
            lt.AddFile("\\" + file, "/" + file);
        }

        Logger.Info("Writing files to tape...");
        await lt.Commit(LtfsTaskQueueType.Write);

        lt.TapeDrive?.Unthread();

        var diag = lt.TapeDrive?.ReadDiagCM();
        if (diag != null)
        {
            var timestamp = DateTime.Now;
            var fileName = $"{barcode}_{timestamp:yyyyMMdd}_{timestamp:HHmmss}.{timestamp:FFFFFFF}.cmbin";
            await File.WriteAllBytesAsync(fileName, diag);
        }

        lt.TapeDrive?.Unload();

        Logger.Info("Done.");
        lt.TapeDrive?.Dispose();

    }
}
