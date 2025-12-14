using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Text;
using LtoTape;
using System.Linq;

namespace TapeDrive;

public partial class LTOTapeDrive : TapeDriveBase, IDisposable
{
    private SafeFileHandle? _handle;
    private readonly string _devicePath;

    public bool IsOpened => _handle != null && !_handle.IsInvalid;

    public bool AllowPartition { get; set; } = true;

    public string Vendor { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;


    public LTOTapeDrive(string devicePath = @"\\.\Tape0", bool open = true)
    {
        _devicePath = devicePath;

        if (open)
        {
            Open(devicePath);
        }
    }

    public override void Dispose()
    {
        Close();
    }

    private void Open(string devicePath)
    {
        _handle = NativeMethods.CreateFile(devicePath,
            0xC0000000, // GENERIC_READ | GENERIC_WRITE
            3,          // FILE_SHARE_READ | FILE_SHARE_WRITE
            IntPtr.Zero, 3, 0, IntPtr.Zero);

        if (_handle.IsInvalid)
        {
            throw new IOException($"Failed to open tape drive {_devicePath}, Win32Error={Marshal.GetLastWin32Error()}");
        }
    }

    public void Close()
    {
        if (_handle != null && !_handle.IsInvalid)
        {
            _handle.Close();
            _handle = null;
        }
    }

    public override bool GetInquiry()
    {
        byte[] data = ScsiRead([0x12, 0x01, 0x80, 0, 0x04, 0], 4);
        var pageLen = data[3] + 4;

        if (pageLen == 4)
            return false;

        var pageData = ScsiRead([0x12, 0x01, 0x80, 0, (byte)(pageLen & 0xFF), 0], pageLen);

        Revision = Encoding.ASCII.GetString(pageData, 4, pageLen - 4).Trim();
        pageData = ScsiRead([0x12, 0, 0, 0, 0x60, 0], 0x60);

        Vendor = Encoding.ASCII.GetString(pageData, 8, 8).Trim();
        Product = Encoding.ASCII.GetString(pageData, 16, 16).Trim();
        return true;
    }

    public override bool TestUnitReady()
    {
        ScsiCommand([0, 0, 0, 0, 0, 0], SCSI_IOCTL_DATA_IN);
        return Sense[0] == 0;
    }

    public override void Load() => ScsiCommand([0x1b, 0, 0, 0, 0x01, 0]);

    public override void Unload() => ScsiCommand([0x1b, 0, 0, 0, 0x00, 0]);

    public override bool Rewind() => ScsiCommand([0x01, 0, 0, 0, 0, 0], SCSI_IOCTL_DATA_IN);

    public override bool Unthread() => ScsiCommand([0x1b, 0, 0, 0, 0x0a, 0], SCSI_IOCTL_DATA_IN);

    public override byte[] ReadBuffer(byte bufferID, byte mode = 2)
    {
        byte[] lendata = ScsiRead([0x3c, 3, bufferID, 0, 0, 0, 0, 0, 4, 0], 4);

        int bufferLen = 0;
        for (int i = 0; i < lendata.Length; i++)
        {
            bufferLen = (bufferLen << 8) | lendata[i];
        }

        byte[] dumpdata = ScsiRead([0x3c, mode, bufferID, 0, 0, 0, lendata[1], lendata[2], lendata[3], 0], bufferLen);

        File.WriteAllBytes($"buffer_{bufferID:X2}.bin", dumpdata);

        return dumpdata;
    }

    public override byte[] ReadDiagCM(int len10h = 0)
    {
        ScsiWrite(
            [0x1d, 0x11, 0, 0, 0x14, 0],
            [0xb0, 0, 0, 0x10, 0, 0, 0, 0, 0, 0, 0x1f, 0xe0, 0, 0, 0, 0x15, 0, 0, 0, 0x08]
            );

        int len = 0xc7a2;

        if (len10h == 0)
            len10h = ReadBuffer(0x10).Length;

        if (len10h > 0)
            len = 6 + (len10h / 16) * 50 + (len10h % 16) * 3;

        var data = ScsiRead([0x1c, 1, 0xb0, (byte)((len >> 8) & 0xff), (byte)((len & 0xff)), 0], 0xc7a2);

        File.WriteAllBytes("cm_diag_raw.bin", data);

        var bufferdgtext = Encoding.ASCII.GetString(data, 6, data.Length - 6);
        bufferdgtext = bufferdgtext.Replace("\0", "").Trim();
        var lines = bufferdgtext.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        List<byte> bytes = new();

        foreach (var line in lines)
        {
            if (line.Length <= 2)
                continue;

            foreach (var part in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Length != 2)
                    continue;
                if (byte.TryParse(part, System.Globalization.NumberStyles.HexNumber, null, out var b))
                {
                    bytes.Add(b);
                }
            }

            //Console.WriteLine(line);
        }

        File.WriteAllBytes("cm_diag.bin", bytes.ToArray());

        return bytes.ToArray();
    }




    public override string ReadBarCode() =>
        GetMAMAttribute(0x0806).DataAsString.TrimEnd();

    public override string ReadAppInfo()
    {
        return GetMAMAttribute(0x0800).DataAsString.TrimEnd() + " " +
            GetMAMAttribute(0x0801).DataAsString.TrimEnd() + " " +
            GetMAMAttribute(0x0802).DataAsString.TrimEnd();
    }

    public override ulong ReadRemainingCapacity() => GetMAMAttribute(0x0000).AsUInt64;

    public override byte ReadDensityCode()
    {
        var result = ScsiRead([0x1a, 0, 0, 0, 0x0c, 0], 12);
        return result[4];
    }

    public override bool SetBarcode(string barcode)
    {
        return SetMAMAttribute(0x0806, barcode.ToUpperInvariant(), 32);
    }

    public override bool SetBlockSize(ulong blockSize)
    {
        byte densityCode = ReadDensityCode();
        blockSize = Math.Min(blockSize, GlobalBlockSizeLimit);
        // SCSI block length in Mode Select block descriptor is 3 bytes.
        // Ensure we don't exceed 24-bit value and send only the low 3 bytes (big-endian).
        uint bs = (uint)Math.Min(blockSize, 0xFFFFFFu);
        byte[] b = BigEndianBitConverter.GetBytes(bs); // 4 bytes big-endian

        byte[] data = [
            0, 0, 0x10, 8,  // Mode parameter header

            densityCode, 0, 0, 0,
            0, b[1], b[2], b[3]
        ];

        byte[] cdb = [0x15, 0x10, 0, 0, (byte)data.Length, 0];

        return ScsiWrite(cdb, data);
    }

    public override bool WriteFileMarks(uint number) => ScsiWrite([0x10, (byte)Math.Min(number, 1), (byte)((number >> 16) & 0xff), (byte)((number >> 8) & 0xff), (byte)(number & 0xff), 0], []);

    public override bool WriteFileMark() => WriteFileMarks(1);

    public override bool Flush() => WriteFileMarks(0);

    public override bool PreventMediaRemoval() => ScsiCommand([0x1e, 0, 0, 0, 1, 0], SCSI_IOCTL_DATA_IN);

    public override bool AllowMediaRemoval() => ScsiCommand([0x1e, 0, 0, 0, 0, 0], SCSI_IOCTL_DATA_IN);

    public override bool ReleaseUnit() => ScsiCommand([0x17, 0, 0, 0, 0, 0], SCSI_IOCTL_DATA_IN);

    public override bool ReserveUnit() => ScsiCommand([0x16, 0, 0, 0, 0, 0], SCSI_IOCTL_DATA_IN);

    public override bool SetCapacity(ushort capacity)
    {
        return ScsiCommand([0x0b, 0, 0, (byte)((capacity >> 8) & 0xff), (byte)(capacity & 0xff), 0]);
    }

    public override bool InitTape() => ScsiCommand([0x04, 0, 0, 0, 0, 0], SCSI_IOCTL_DATA_IN);

    public override bool SelectPartitionMode(byte[] modeData, byte maxPartitions, ushort p0size, ushort p1size)
    {
        byte[] cdb = [0x15, 0x10, 0, 0, 0x10, 0];
        byte[] data = [
            .. new byte[] {0, 0, 0x10, 0, 0x11, 0x0a, maxPartitions, 1 },
            .. modeData[4..8], // 4 to 7
            .. BigEndianBitConverter.GetBytes(p0size),
            .. BigEndianBitConverter.GetBytes(p1size)
            ];
        return ScsiWrite(cdb, data);
    }
    
    public override byte[] ModeSense(byte pageID, bool skipHeader = true)
    {
        byte[] header = ScsiRead([0x1a, 0, pageID, 0, 4, 0], 4);
        if (header.Length == 0)
            return [];

        byte pageLength = header[0];
        if (pageLength == 0)
            return [];

        byte descriptorLength = header[3];

        if (skipHeader)
            return ScsiRead([0x1a, 0, pageID, 0, (byte)(pageLength + 1), 0], pageLength + 1)[(4 + descriptorLength)..];
        else
            return ScsiRead([0x1a, 0, pageID, 0, (byte)(pageLength + 1), 0], pageLength + 1);
    }


    public override BlockLimit ReadBlockLimit()
    {
        byte[] data = ScsiRead([5, 0, 0, 0, 0, 0], 6);
        return new BlockLimit
        {
            MaxBlockLength = (UInt64)((data[1] << 16) | (data[2] << 8) | data[3]),
            MinBlockLength = (UInt64)((data[4] << 8) | data[5])
        };
    }


    public override bool SetEncryption(byte[]? encryptionKey = null)
    {
        var param = Array.Empty<byte>();
        byte[] cdb = [0xb5, 0x20, 0, 0x10, 0, 0, 0, 0, 0, 0x34, 0, 0];

        if (encryptionKey is not null && encryptionKey.Length == 32)
        {
            param = [
                .. new byte[] {
                    0, 0x10, 0, 0x30, 0x40, 0x34, 0x02, 0x03, 0x01, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0x20 },
                .. encryptionKey
            ];
            return ScsiWrite(cdb, param);
        }
        else
        {
            encryptionKey = new byte[32];
            param = [
                .. new byte[] {
                    0, 0x10, 0, 0x30, 0x40, 0, 0, 0, 0x01, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0x20 },
                .. encryptionKey
            ];
            return ScsiWrite(cdb, param);
        }

    }

}

public class BlockLimit
{
    public UInt64 MaxBlockLength { get; set; }
    public UInt64 MinBlockLength { get; set; }
}

public enum AttributeFormat
{
    Binary = 0,
    Ascii = 1,
    Text = 2,
    Reserved = 3,
};
