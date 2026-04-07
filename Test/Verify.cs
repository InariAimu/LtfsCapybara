
using System;
using System.Threading.Tasks;
using Ltfs;
using TapeDrive;

namespace Test;

public class Verify
{
    public static async Task Test(Ltfs.Ltfs lt)
    {
        Logger.Info("Loading LTFS from tape...");
        lt.LoadTape();

        Logger.Info("Reading LTFS Index...");
        lt.ReadLtfs();

        string folder = "";
        lt.AddVerifyTask("/" + folder);

        Logger.Info("Verifying files...");
        await lt.Commit(LtfsTaskQueueType.Verify);

        lt.TapeDrive?.Unthread();

        Logger.Info("Done.");
        lt.TapeDrive?.Dispose();
    }
}
