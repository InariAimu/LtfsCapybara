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
    public byte[] GetMAMAttributeBytes(ushort pageCode, byte PartitionNumber = 0)
    {
        byte PageCodeH = (byte)((pageCode >> 8) & 0xff);
        byte PageCodeL = (byte)(pageCode & 0xff);

        byte[] result = [];
        int dataLen = 0;
        byte[] db = BigEndianBitConverter.GetBytes(dataLen + 9);
        byte[] cdbData =
        [
            0x8c, 0, 0, 0, 0, 0, 0, PartitionNumber, PageCodeH, PageCodeL,
            db[0], db[1], db[2], db[3], 0, 0
        ];

        byte[] BCArray = new byte[dataLen + 9];
        IntPtr dataPtr = Marshal.AllocHGlobal(dataLen + 9);
        Marshal.Copy(BCArray, 0, dataPtr, BCArray.Length);

        IOCtlDirect(cdbData, dataPtr, (uint)(dataLen + 9), SCSI_IOCTL_DATA_IN, 600);

        Marshal.Copy(dataPtr, BCArray, 0, dataLen + 9);

        dataLen = ((int)(BCArray[7]) << 8) | BCArray[8];

        if (dataLen > 0)
        {
            IntPtr dataPtr2 = Marshal.AllocHGlobal(dataLen + 9);
            byte[] BCArray2 = new byte[dataLen + 9];
            Marshal.Copy(BCArray2, 0, dataPtr2, BCArray2.Length);

            byte[] db2 = BigEndianBitConverter.GetBytes(dataLen + 9);
            cdbData =
            [
                0x8c, 0, 0, 0, 0, 0, 0, PartitionNumber, PageCodeH, PageCodeL,
                db2[0], db2[1], db2[2], db2[3], 0, 0
            ];

            IOCtlDirect(cdbData, dataPtr2, (uint)(dataLen + 9), SCSI_IOCTL_DATA_IN);

            Marshal.Copy(dataPtr2, BCArray2, 0, dataLen + 9);
            result = BCArray2[9..];

            Marshal.FreeHGlobal(dataPtr2);
        }
        Marshal.FreeHGlobal(dataPtr);

        return result;
    }


    public MAMAttribute GetMAMAttribute(ushort PageCode, byte PartitionNumber = 0)
    {
        byte[] result = GetMAMAttributeBytes(PageCode, PartitionNumber);

        MAMAttribute mam = new MAMAttribute
        {
            ID = PageCode,
            RawData = result
        };
        return mam;
    }


    public bool SetMAMAttribute(ushort pageID, byte[] data, AttributeFormat format = AttributeFormat.Binary, byte partition = 0)
    {
        int len = data.Length + 9;
        byte[] paramLen = BigEndianBitConverter.GetBytes(len);
        byte[] dataLen = BigEndianBitConverter.GetBytes(data.Length);

        byte[] cdb = [0x8d, 0, 0, 0, 0, 0, 0, partition, 0, 0,
                           paramLen[0], paramLen[1], paramLen[2], paramLen[3], 0, 0];
        byte[] param = [paramLen[0], paramLen[1], paramLen[2], paramLen[3],
                            (byte)((pageID >> 8) & 0xff), (byte)(pageID & 0xff),
                            (byte)format, dataLen[2], dataLen[3]];
        var succ = ScsiWrite(cdb, [.. param, .. data]);

        var msg = LTOTapeDrive.ParseSenseData(Sense);
        return succ;
    }


    public bool SetMAMAttribute(ushort pageID, string text, int textMaxLength, byte partition = 0)
    {
        byte[] data = Encoding.ASCII.GetBytes(text.PadRight(textMaxLength)[..textMaxLength]);
        return SetMAMAttribute(pageID, data, AttributeFormat.Ascii, partition);
    }


    public bool WriteVCI(UInt64 generation, UInt64 block0, UInt64 block1, Guid uuid, byte extraPartitionCount)
    {
        byte[] vciData = [];
        byte[] vci = GetMAMAttributeBytes(0x0009);
        if (vci.Length == 0)
            return false;

        if (extraPartitionCount > 0)
        {
            vciData = [.. new byte[] { 8, 0, 0, 0, 0 },
                            .. vci[^4..],
                            .. BigEndianBitConverter.GetBytes(generation),
                            .. BigEndianBitConverter.GetBytes(block0),
                            .. new byte[] { 0, 0x2b, 0x4c, 0x54, 0x46, 0x53, 0 },
                            .. Encoding.ASCII.GetBytes(uuid.ToString().PadRight(36)[..36]),
                            .. new byte[] { 0, 1 }];

            bool result = SetMAMAttribute(0x080c, vciData, AttributeFormat.Binary, 0);
            if (!result)
                return false;
        }

        vciData = [.. new byte[] { 8, 0, 0, 0, 0 },
                        .. vci[^4..],
                        .. BigEndianBitConverter.GetBytes(generation),
                        .. BigEndianBitConverter.GetBytes(block1),
                        .. new byte[] { 0, 0x2b, 0x4c, 0x54, 0x46, 0x53, 0 },
                        .. Encoding.ASCII.GetBytes(uuid.ToString().PadRight(36)[..36]),
                        .. new byte[] { 0, 1 }];

        return SetMAMAttribute(0x080c, vciData, AttributeFormat.Binary, extraPartitionCount);
    }


}
