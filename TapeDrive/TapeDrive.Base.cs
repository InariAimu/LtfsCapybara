using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LtoTape;

namespace TapeDrive;

/// <summary>
/// Base class for tape drive implementations. Provides noop/default implementations
/// of I/O operations so tests can inherit and run logic without touching hardware.
/// Real device implementations (like `LTOTapeDrive`) should override members.
/// </summary>
public abstract class TapeDriveBase : IDisposable
{
    public virtual byte[] Sense { get; protected set; } = new byte[64];

    public virtual void ResetSense()
    {
        Sense = new byte[64];
    }

    public virtual bool IOCtlDirect(byte[] cdb, IntPtr dataBuffer, uint bufferLength, byte dataIn = 0, uint timeoutSeconds = 600)
    {
        // No real IO in base class; tests can override to simulate results.
        return true;
    }

    public virtual IntPtr ScsiReadRaw(byte[] cdb, int readLength, uint timeoutSeconds = 600)
    {
        // Return an allocated zeroed buffer that caller owns and should free if needed.
        IntPtr dataPtr = Marshal.AllocHGlobal(readLength);
        if (readLength > 0)
            Marshal.Copy(new byte[readLength], 0, dataPtr, readLength);
        return dataPtr;
    }

    public virtual byte[] ScsiRead(byte[] cdb, int readLength, uint timeoutSeconds = 600)
    {
        return new byte[Math.Max(0, readLength)];
    }

    public virtual bool ScsiWrite(byte[] cdb, byte[]? data, uint timeoutSeconds = 600)
    {
        return true;
    }

    public virtual bool ScsiCommand(byte[] cdb, byte inout = 0, uint timeoutSeconds = 600)
    {
        return true;
    }

    public virtual Task<byte[]> ScsiReadAsync(byte[] cdb, int readLength, uint timeoutSeconds = 600)
    {
        return Task.FromResult(ScsiRead(cdb, readLength, timeoutSeconds));
    }

    public virtual Task<bool> ScsiWriteAsync(byte[] cdb, byte[]? data, uint timeoutSeconds = 600)
    {
        return Task.FromResult(ScsiWrite(cdb, data, timeoutSeconds));
    }

    // Higher-level operations exposed by LTOTapeDrive used by the rest of the codebase.
    // Provide virtual defaults so tests can override only what they need.
    public virtual bool TestUnitReady() => true;
    public virtual bool GetInquiry() => true;
    public virtual void Load() { }
    public virtual void Unload() { }
    public virtual bool Rewind() => true;
    public virtual bool Unthread() => true;

    public virtual byte[] ReadBuffer(byte bufferID, byte mode = 2) => Array.Empty<byte>();
    public virtual byte[] ReadDiagCM(int len10h = 0) => Array.Empty<byte>();

    public virtual string ReadBarCode() => string.Empty;
    public virtual string ReadAppInfo() => string.Empty;
    public virtual ulong ReadRemainingCapacity() => 0;
    public virtual byte ReadDensityCode() => 0;

    public virtual bool SetBarcode(string barcode) => true;
    public virtual bool SetBlockSize(ulong blockSize) => true;

    public virtual bool WriteFileMarks(uint number) => true;
    public virtual bool WriteFileMark() => WriteFileMarks(1);
    public virtual bool Flush() => true;

    public virtual bool PreventMediaRemoval() => true;
    public virtual bool AllowMediaRemoval() => true;
    public virtual bool ReleaseUnit() => true;
    public virtual bool ReserveUnit() => true;

    public virtual bool SetCapacity(ushort capacity) => true;
    public virtual bool InitTape() => true;

    public virtual bool SelectPartitionMode(byte[] modeData, byte maxPartitions, ushort p0size, ushort p1size) => true;

    public virtual byte[] ModeSense(byte pageID, bool skipHeader = true) => Array.Empty<byte>();

    public virtual BlockLimit ReadBlockLimit() => new BlockLimit { MaxBlockLength = 0, MinBlockLength = 0 };

    public virtual bool SetEncryption(byte[]? encryptionKey = null) => true;

    public virtual ushort Locate(ulong blockAddress, byte partitionNumber, LocateType locateType) => 0;
    public virtual byte[] ReadBlock(uint blockSizeLimit = 0x080000, bool truncate = false) => Array.Empty<byte>();
    public virtual ushort Space6(int count, LocateType code) => 0;
    public virtual PositionData ReadPosition() => new PositionData();
    public virtual void ReadToFileMarkToLocalFile(string filename, int blockSizeLimit = 0x080000) { }
    public virtual byte[] ReadToFileMark(int blockSizeLimit = 0x080000) => Array.Empty<byte>();
    public virtual bool ReadFileMark() => true;

    public virtual bool Write(byte[] data) => true;
    public virtual bool PreAllocWriteBuffer(int blockSize) => true;
    public virtual void ReleaseWriteBuffer() { }
    public virtual bool BufferedWrite(ReadOnlyMemory<byte> data, int blockSize) => true;

    // Overloads and helpers expected by callers
    public virtual bool Write(byte[] data, int blockSize) => Write(data);

    public virtual bool SetMAMAttribute(ushort pageID, string text, int textMaxLength, byte partition = 0) => true;

    public virtual void Dispose() { }

    // MAM attribute helpers
    public virtual byte[] GetMAMAttributeBytes(ushort pageCode, byte PartitionNumber = 0) => Array.Empty<byte>();
    public virtual MAMAttribute GetMAMAttribute(ushort PageCode, byte PartitionNumber = 0) => new MAMAttribute { ID = PageCode, RawData = Array.Empty<byte>() };
    public virtual bool SetMAMAttribute(ushort pageID, byte[] data, AttributeFormat format = AttributeFormat.Binary, byte partition = 0) => true;

    // Misc
    public virtual uint GlobalBlockSizeLimit { get; set; } = 0x00080000;
}
