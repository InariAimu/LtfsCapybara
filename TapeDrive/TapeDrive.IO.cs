using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using LtoTape;

namespace TapeDrive;
public partial class LTOTapeDrive
{
    public override uint GlobalBlockSizeLimit { get; set; } = 0x00080000;


    public override ushort Locate(ulong blockAddress, byte partitionNumber, LocateType locateType)
    {
        if (AllowPartition || locateType != 0)
        {
            byte cp = 0;
            if (ReadPosition().PartitionNumber != partitionNumber)
                cp = 1;

            ScsiRead([
                    0x92, (byte)(((int)locateType << 3) | (cp << 1)), 0, partitionNumber,
                    (byte)((blockAddress >> 56) & 0xff),
                    (byte)((blockAddress >> 48) & 0xff),
                    (byte)((blockAddress >> 40) & 0xff),
                    (byte)((blockAddress >> 32) & 0xff),
                    (byte)((blockAddress >> 24) & 0xff),
                    (byte)((blockAddress >> 16) & 0xff),
                    (byte)((blockAddress >> 8) & 0xff),
                    (byte)((blockAddress) & 0xff),
                    0, 0, 0, 0],
                0, 600
            );
        }
        else
        {
            ScsiRead([
                    0x2b, 0, 0,
                    (byte)((blockAddress >> 24) & 0xff),
                    (byte)((blockAddress >> 16) & 0xff),
                    (byte)((blockAddress >> 8) & 0xff),
                    (byte)((blockAddress) & 0xff),
                    0, 0, 0],
                0, 600
            );
        }

        ushort addCode = BigEndianBitConverter.ToUInt16(Sense, 12);
        if (addCode != 0 && ((Sense[2] & 0x0f) != 8))
        {
            if (locateType == LocateType.EOD)
            {
                if (!ReadPosition().EOD)
                    ScsiCommand([0x11, 3, 0, 0, 0, 0], SCSI_IOCTL_DATA_IN);
            }
            else if (locateType == LocateType.FileMark)
            {
                Locate(0, 0, LocateType.Block);
                Space6((int)blockAddress, LocateType.FileMark);
            }
            else
            {
                ScsiRead(
                [0x92, (byte)((int)locateType << 3), 0, 0,
                    (byte)((blockAddress >> 56) & 0xff),
                    (byte)((blockAddress >> 48) & 0xff),
                    (byte)((blockAddress >> 40) & 0xff),
                    (byte)((blockAddress >> 32) & 0xff),
                    (byte)((blockAddress >> 24) & 0xff),
                    (byte)((blockAddress >> 16) & 0xff),
                    (byte)((blockAddress >> 8) & 0xff),
                    (byte)((blockAddress) & 0xff),
                    0, 0, 0, 0
                ], 64);

            }
            addCode = BigEndianBitConverter.ToUInt16(Sense, 12);
        }
        else
        {
            addCode = 0;
        }
        return addCode;
    }

    public override byte[] ReadBlock(uint blockSizeLimit = 0x080000, bool truncate = false)
    {
        blockSizeLimit = Math.Min(blockSizeLimit, GlobalBlockSizeLimit);

        IntPtr rawDataU = IntPtr.Zero;
        int diffBytes = 0;
        int dataLen = 0;

        rawDataU = ScsiReadRaw([8, 0, (byte)((blockSizeLimit >> 16) & 0xff), (byte)((blockSizeLimit >> 8) & 0xff), (byte)((blockSizeLimit) & 0xff), 0], (int)blockSizeLimit, 600);

        diffBytes = BigEndianBitConverter.ToInt32(Sense, 3);

        if (truncate)
            diffBytes = Math.Max(diffBytes, 0);

        dataLen = (int)Math.Min(blockSizeLimit, blockSizeLimit - diffBytes);
        if (!truncate && diffBytes < 0 && (blockSizeLimit - diffBytes) <= GlobalBlockSizeLimit)
        {
            Marshal.FreeHGlobal(rawDataU);
            PositionData pd = new();
            Locate(pd.BlockNumber - 1, pd.PartitionNumber, LocateType.Block);
            return ReadBlock((uint)(blockSizeLimit - diffBytes), truncate);
        }

        byte[] rawData = new byte[dataLen];
        Marshal.Copy(rawDataU, rawData, 0, (int)Math.Min(blockSizeLimit, dataLen));
        Marshal.FreeHGlobal(rawDataU);

        return rawData;
    }

    public override ushort Space6(int count, LocateType code)
    {
        byte[] c = BigEndianBitConverter.GetBytes(count);
        ScsiRead([0x11, (byte)code, c[1], c[2], c[3], 0], 64);
        ushort addCode = BigEndianBitConverter.ToUInt16(Sense, 12);
        return addCode;
    }

    public override PositionData ReadPosition()
    {
        byte[] param;
        PositionData result = new();

        ResetSense();
        if (AllowPartition)
        {
            param = ScsiRead([0x34, 0x06, 0, 0, 0, 0, 0, 0, 0, 0], 32);
            result.BOP = ((param[0] >> 7) & 1) > 0;
            result.EOP = ((param[0] >> 6) & 1) > 0;
            result.MPU = ((param[0] >> 3) & 1) > 0;
            result.PartitionNumber = (byte)BigEndianBitConverter.ToUInt32(param, 4);
            result.BlockNumber = BigEndianBitConverter.ToUInt64(param, 8);
            result.FileNumber = BigEndianBitConverter.ToUInt64(param, 16);
            result.SetNumber = BigEndianBitConverter.ToUInt64(param, 24);
        }
        else
        {
            param = ScsiRead([0x34, 0, 0, 0, 0, 0, 0, 0, 0, 0], 32);
            result.BOP = ((param[0] >> 7) & 1) > 0;
            result.EOP = ((param[0] >> 6) & 1) > 0;
            result.BlockNumber = BigEndianBitConverter.ToUInt64(param, 4);
        }
        if (Sense != null && Sense.Length >= 14)
        {
            result.AddSenseKey = BigEndianBitConverter.ToUInt16(Sense, 12);
        }

        return result;
    }

    public override void ReadToFileMarkToLocalFile(string filename, int blockSizeLimit = 0x080000)
    {
        byte[] param = ScsiRead([0x34, 0, 0, 0, 0, 0, 0, 0, 0, 0], 20);
        blockSizeLimit = (int)Math.Min(blockSizeLimit, GlobalBlockSizeLimit);

        using FileStream fs = new(filename, FileMode.Create, FileAccess.Write);

        while (true)
        {
            var data = ReadBlock((uint)blockSizeLimit, true);
            ushort addCode = BigEndianBitConverter.ToUInt16(Sense, 12);
            if (data.Length > 0)
            {
                fs.Write(data, 0, data.Length);
            }
            if (addCode >= 1 && addCode != 4)
                break;
        }
    }

    public override byte[] ReadToFileMark(int blockSizeLimit = 0x080000)
    {
        byte[] param = ScsiRead([0x34, 0, 0, 0, 0, 0, 0, 0, 0, 0], 20);
        blockSizeLimit = (int)Math.Min(blockSizeLimit, GlobalBlockSizeLimit);

        using MemoryStream fs = new MemoryStream(blockSizeLimit);

        while (true)
        {
            var data = ReadBlock((uint)blockSizeLimit, true);
            ushort addCode = BigEndianBitConverter.ToUInt16(Sense, 12);
            if (data.Length > 0)
            {
                fs.Write(data, 0, data.Length);
            }
            if (addCode >= 1 && addCode != 4)
                break;
        }

        return fs.ToArray();
    }

    public override bool ReadFileMark()
    {
        byte[] data = ReadBlock();
        if (data.Length == 0)
            return true;

        var p = ReadPosition();
        if (!AllowPartition)
        {
            Space6(-1, LocateType.Block);
        }
        else
        {
            Locate(p.BlockNumber - 1, p.PartitionNumber, LocateType.Block);
        }
        return false;
    }

    /// <summary>
    /// use this when data length less than block size limit
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public override bool Write(byte[] data)
    {
        int len = data.Length;
        byte[] lenb = BigEndianBitConverter.GetBytes(len);

        return ScsiWrite([0x0a, 0, lenb[1], lenb[2], lenb[3], 0], data);
    }

    IntPtr writeBufferPtr = IntPtr.Zero;

    public override bool PreAllocWriteBuffer(int blockSize)
    {
        if (writeBufferPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(writeBufferPtr);
        }
        writeBufferPtr = Marshal.AllocHGlobal(blockSize);
        return writeBufferPtr != IntPtr.Zero;
    }

    public override void ReleaseWriteBuffer()
    {
        if (writeBufferPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(writeBufferPtr);
            writeBufferPtr = IntPtr.Zero;
        }
    }

    public override bool Write(byte[] data, int blockSize)
    {
        if (data.Length <= blockSize)
        {
            return Write(data);
        }

        if (writeBufferPtr == IntPtr.Zero)
        {
            PreAllocWriteBuffer(blockSize);
        }

        for (int pos = 0; pos < data.Length;)
        {
            int len = Math.Min(blockSize, data.Length - pos);
            Marshal.Copy(data, pos, writeBufferPtr, len);

            byte[] lenb = BigEndianBitConverter.GetBytes(len);

            bool succ = IOCtlDirect([0x0a, 0, lenb[1], lenb[2], lenb[3], 0], writeBufferPtr, (uint)len, SCSI_IOCTL_DATA_OUT, 600);

            if (!succ)
            {
                Marshal.FreeHGlobal(writeBufferPtr);
                return false;
            }

            pos += len;
        }

        return true;
    }


    public override bool BufferedWrite(ReadOnlyMemory<byte> data, int blockSize)
    {
        if (data.Length <= blockSize)
        {
            return Write(data.Span.ToArray());
        }

        var writeBufferPtrMao = data.Pin();

        for (int pos = 0; pos < data.Length;)
        {
            int len = Math.Min(blockSize, data.Length - pos);
            byte[] lenb = BigEndianBitConverter.GetBytes(len);

            unsafe
            {
                bool succ = IOCtlDirect(
                    cdb: [0x0a, 0, lenb[1], lenb[2], lenb[3], 0], 
                    dataBuffer: (IntPtr)writeBufferPtrMao.Pointer + pos, 
                    (uint)len, SCSI_IOCTL_DATA_OUT, 600);

                if (!succ)
                    return false;
            }

            pos += len;
        }

        return true;
    }
}
