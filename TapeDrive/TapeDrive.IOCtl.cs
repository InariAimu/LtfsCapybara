using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TapeDrive;

public partial class LTOTapeDrive
{
    public byte[] Sense { get; private set; } = new byte[64];

    public void ResetSense()
    {
        Sense = new byte[64];
    }

    private const uint IOCTL_SCSI_PASS_THROUGH = 0x4D004;
    private const uint IOCTL_SCSI_PASS_THROUGH_DIRECT = 0x4D014;

    private const int SENSE_LEN = 64;
    private const int CDB_LEN = 16;

    /// <summary>
    /// Data from device to memory
    /// </summary>
    private const byte SCSI_IOCTL_DATA_IN = 1;

    /// <summary>
    /// Data from memory to device
    /// </summary>
    private const byte SCSI_IOCTL_DATA_OUT = 0;

    /// <summary>
    /// Unspecified data direction
    /// </summary>
    private const byte SCSI_IOCTL_DATA_UNSPECIFIED = 2;


    [StructLayout(LayoutKind.Sequential)]
    private struct SCSI_PASS_THROUGH
    {
        public ushort Length;
        public byte ScsiStatus;
        public byte PathId;
        public byte TargetId;
        public byte Lun;
        public byte CdbLength;
        public byte SenseInfoLength;
        public byte DataIn;
        public uint DataTransferLength;
        public uint TimeOutValue;
        public IntPtr DataBuffer;
        public uint SenseInfoOffset;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CDB_LEN)]
        public byte[] Cdb;
    }


    [StructLayout(LayoutKind.Sequential)]
    private struct SCSI_PASS_THROUGH_WITH_BUFFERS
    {
        public SCSI_PASS_THROUGH spt;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SENSE_LEN)]
        public byte[] Sense;
    }

    private SCSI_PASS_THROUGH_WITH_BUFFERS _sptwb = new()
    {
        spt = new SCSI_PASS_THROUGH
        {
            Length = (ushort)Marshal.SizeOf(typeof(SCSI_PASS_THROUGH)),
            CdbLength = 16,
            SenseInfoLength = SENSE_LEN,
            DataIn = SCSI_IOCTL_DATA_OUT,
            DataTransferLength = 0,
            DataBuffer = IntPtr.Zero,
            TimeOutValue = 600,
            Cdb = new byte[CDB_LEN]
        },
        Sense = new byte[SENSE_LEN],
    };

    private readonly uint senseInfoOffset = (uint)Marshal.OffsetOf(typeof(SCSI_PASS_THROUGH_WITH_BUFFERS), "Sense").ToInt32();

    private readonly int sptSize = Marshal.SizeOf(typeof(SCSI_PASS_THROUGH_WITH_BUFFERS));

    IntPtr bufferPtr = IntPtr.Zero;

    public bool IOCtlDirect(byte[] cdb, IntPtr dataBuffer, uint bufferLength, byte dataIn = SCSI_IOCTL_DATA_OUT, uint timeoutSeconds = 600)
    {
        _sptwb.spt.CdbLength = (byte)cdb.Length;
        _sptwb.spt.DataIn = dataIn;
        _sptwb.spt.DataTransferLength = bufferLength;
        _sptwb.spt.DataBuffer = dataBuffer;
        _sptwb.spt.TimeOutValue = timeoutSeconds;
        _sptwb.spt.SenseInfoOffset = senseInfoOffset;

        Array.Copy(cdb, _sptwb.spt.Cdb, cdb.Length);

        if (bufferPtr == IntPtr.Zero)
            bufferPtr = Marshal.AllocHGlobal((int)sptSize);

        try
        {
            Marshal.StructureToPtr(_sptwb, bufferPtr, false);
            int bytesReturned = 0;

            bool result = NativeMethods.DeviceIoControl(_handle, IOCTL_SCSI_PASS_THROUGH_DIRECT,
                bufferPtr, sptSize, bufferPtr, sptSize, ref bytesReturned, IntPtr.Zero);

            if (result)
            {
                var output = Marshal.PtrToStructure<SCSI_PASS_THROUGH_WITH_BUFFERS>(bufferPtr);

                Sense = output.Sense;
            }
            else
            {
                ResetSense();
            }

            return result;
        }
        catch
        {
            Marshal.FreeHGlobal(bufferPtr);
            bufferPtr = IntPtr.Zero;
            return false;
        }
    }

    public IntPtr ScsiReadRaw(byte[] cdb, int readLength, uint timeoutSeconds = 600)
    {
        byte[] data = new byte[readLength];
        IntPtr dataPtr = Marshal.AllocHGlobal(readLength);
        Marshal.Copy(data, 0, dataPtr, readLength);

        IOCtlDirect(cdb, dataPtr, (uint)readLength, SCSI_IOCTL_DATA_IN, timeoutSeconds);

        return dataPtr;
    }

    public byte[] ScsiRead(byte[] cdb, int readLength, uint timeoutSeconds = 600)
    {
        byte[] data = new byte[readLength];
        IntPtr dataPtr = ScsiReadRaw(cdb, readLength, timeoutSeconds);

        Marshal.Copy(dataPtr, data, 0, readLength);
        Marshal.FreeHGlobal(dataPtr);

        return data;
    }

    public bool ScsiWrite(byte[] cdb, byte[]? data, uint timeoutSeconds = 600)
    {
        int dataLength = data?.Length ?? 128;
        IntPtr dataPtr = Marshal.AllocHGlobal(dataLength);

        if (data != null)
            Marshal.Copy(data, 0, dataPtr, dataLength);

        bool result = IOCtlDirect(cdb, dataPtr, (uint)dataLength, SCSI_IOCTL_DATA_OUT, timeoutSeconds);

        Marshal.FreeHGlobal(dataPtr);

        return result;
    }

    public bool ScsiCommand(byte[] cdb, byte inout = SCSI_IOCTL_DATA_OUT, uint timeoutSeconds = 600)
    {
        return IOCtlDirect(cdb, IntPtr.Zero, 0, inout, timeoutSeconds);
    }

    public Task<byte[]> ScsiReadAsync(byte[] cdb, int readLength, uint timeoutSeconds = 600)
    {
        return Task.Run(() => ScsiRead(cdb, readLength, timeoutSeconds));
    }

    public Task<bool> ScsiWriteAsync(byte[] cdb, byte[]? data, uint timeoutSeconds = 600)
    {
        return Task.Run(() => ScsiWrite(cdb, data, timeoutSeconds));
    }

}
