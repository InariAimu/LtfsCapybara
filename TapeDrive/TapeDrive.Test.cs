using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TapeDrive;

/// <summary>
/// Test double for tape drive operations. Methods return success/neutral
/// values so unit tests can exercise higher-level logic without hardware.
/// </summary>
public class FakeTapeDrive : TapeDriveBase
{
    public FakeTapeDrive()
    {
        // Initialize Sense to a harmless value
        Sense = new byte[64];
    }

    public override bool IOCtlDirect(byte[] cdb, IntPtr dataBuffer, uint bufferLength, byte dataIn = 0, uint timeoutSeconds = 600)
    {
        // Do nothing and report success
        return true;
    }

    public override IntPtr ScsiReadRaw(byte[] cdb, int readLength, uint timeoutSeconds = 600)
    {
        if (readLength <= 0)
            return IntPtr.Zero;

        IntPtr ptr = Marshal.AllocHGlobal(readLength);
        if (readLength > 0)
            Marshal.Copy(new byte[readLength], 0, ptr, readLength);
        return ptr;
    }

    public override byte[] ScsiRead(byte[] cdb, int readLength, uint timeoutSeconds = 600)
    {
        return new byte[Math.Max(0, readLength)];
    }

    public override bool ScsiWrite(byte[] cdb, byte[]? data, uint timeoutSeconds = 600)
    {
        return true;
    }

    public override bool ScsiCommand(byte[] cdb, byte inout = 0, uint timeoutSeconds = 600)
    {
        return true;
    }

    public override Task<byte[]> ScsiReadAsync(byte[] cdb, int readLength, uint timeoutSeconds = 600)
    {
        return Task.FromResult(ScsiRead(cdb, readLength, timeoutSeconds));
    }

    public override Task<bool> ScsiWriteAsync(byte[] cdb, byte[]? data, uint timeoutSeconds = 600)
    {
        return Task.FromResult(true);
    }
}
