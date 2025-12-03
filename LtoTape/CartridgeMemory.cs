using System.Text;
using System.Text.RegularExpressions;

using LtoTape.CM;

namespace LtoTape;

public class CartridgeMemory
{
    private byte[] _rawBytes = [];

    private readonly Dictionary<int, PageInfo> _pages = [];

    public Manufacturer Manufacturer { get; set; } = new();
    public MediaManufacturer MediaManufacturer { get; set; } = new();
    public Dictionary<int, UsagePage> UsagePages { get; set; } = [];
    public Dictionary<int, Usage> Usages { get; set; } = [];
    public TapeStatus TapeStatus { get; set; } = new();
    public Dictionary<int, EOD> EODs { get; set; } = [];


    public void FromLcgCmFile(string cmTextFile)
    {
        string[] lines = File.ReadAllLines(cmTextFile);

        bool inRaw = false;
        var hexText = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            if (line.Contains("CM RAW DATA"))
            {
                inRaw = true;
                continue;
            }

            if (inRaw)
            {
                if (line.StartsWith('+') || string.IsNullOrWhiteSpace(line))
                    break;

                string hexPart = line.Substring(0, 58);

                hexText.AppendLine(hexPart);
            }
        }

        MatchCollection matches = Regex.Matches(hexText.ToString(), @"\b[0-9A-Fa-f]{2}\b");

        byte[] data = new byte[matches.Count];
        for (int i = 0; i < matches.Count; i++)
        {
            data[i] = Convert.ToByte(matches[i].Value, 16);
        }

        _rawBytes = data;
    }

    public void SaveToBinaryFile(string binOutputFile)
    {
        File.WriteAllBytes(binOutputFile, _rawBytes);
    }

    public void FromBinaryFile(string fileName)
    {
        _rawBytes = File.ReadAllBytes(fileName);
        _pages.Clear();

        int unProt = 0;
        int offset = 36;

        while (offset < 400)
        {
            UInt16 pageId = (ushort)(BigEndianBitConverter.ToUInt16(_rawBytes, offset) & 0xfff);
            if (pageId == 0xfff)
            {
                if (unProt == 0)
                {
                    unProt = 1;
                    offset = BigEndianBitConverter.ToUInt16(_rawBytes, offset + 2);
                }
                else
                {
                    break;
                }
            }
            else if (pageId == 0x0ffc || pageId == 0x0ffe)
            {
                offset += 4;
            }
            else
            {
                PageInfo pageInfo = new()
                {
                    Version = (byte)((_rawBytes[offset] >> 4) & 0x0f),
                    Offset = BigEndianBitConverter.ToUInt16(_rawBytes, offset + 2),
                    Length = 0
                };
                pageInfo.Length = BigEndianBitConverter.ToUInt16(_rawBytes, pageInfo.Offset + 2);

                _pages[pageId] = pageInfo;
                offset += 4;
            }
        }

        // Parse the cartridge manufacturers page
        PageInfo pi = _pages[1];
        if (pi.Length > 0)
        {
            Manufacturer.Parse(_rawBytes, pi.Offset);
        }

        // Parse the media manufacturers page
        pi = _pages[2];
        if (pi.Length > 0)
        {
            MediaManufacturer.Parse(_rawBytes, pi.Offset);
            if (Manufacturer.Format == "LTO-8")
            {
                if (MediaManufacturer.MfgDate.StartsWith("22"))
                    Manufacturer.ServoBandID = ServoBandID.Legacy_UDIM;
                else if (MediaManufacturer.MfgDate.StartsWith(">>"))
                    Manufacturer.ServoBandID = ServoBandID.Non_UDIM;
            }
        }

        // Parse the usage pages
        int[] offsets = Manufacturer.Gen >= 5 ?
            [32, 36, 44, 52, 56, 60, 62, 64, 66, 80] :
            [24, 28, 36, 44, 48, 52, 54, 56, 58];

        int driveSNLength = Manufacturer.Gen >= 5 ? 16 : 10;
        int length = 0x40;
        pi = _pages[0x0108];
        if (pi.Length > 0)
            length = pi.Length;

        int err = 0;
        string mechRelatedInfoVendorID = "";
        pi = _pages[0x0106];
        if (pi.Length > 0)
        {
            mechRelatedInfoVendorID = Encoding.ASCII.GetString(_rawBytes, pi.Offset + 4, 8).TrimEnd();
        }

        List<UsagePage> usagePages = new();
        for (int i = 0; i < 4; i++)
        {
            int key = i + 0x0108;
            pi = _pages[key];
            PageInfo pi2 = _pages[0x0106];
            bool valid = true;
            if (pi.Offset <= 0 || pi.Length <= 0 || pi2.Offset <= 0 || pi2.Length <= 0)
            {
                valid = false;
            }
            if (valid)
            {
                byte[] buff = new byte[length + 64];
                Array.Copy(_rawBytes, pi.Offset, buff, 0, length);
                Array.Copy(_rawBytes, pi2.Offset + 12 + 64 * i, buff, length, 64);

                usagePages.Add(new UsagePage()
                {
                    Data0 = buff,
                    Data1 = BigEndianBitConverter.ToUInt32(buff, offsets[0]),
                });
            }
            else
            {
                err = 1;
            }
        }

        if (err == 0)
        {
            usagePages.Sort(static (a, b) =>
            {
                bool v = a.Data1 < b.Data1;
                return v ? -1 : 1;
            });

            for (int i = 0; i < 4; i++)
            {
                UsagePages[i] = usagePages[i];
            }

            for (int i = 0; i < 3; i++)
            {
                Usages[i] = new Usage();
                Usages[i].Parse(i, driveSNLength, offsets, UsagePages, Manufacturer, mechRelatedInfoVendorID, length);

            }

        }

        // Parse the "tape status and tape alert flags" page
        pi = _pages[0x0105];
        if (pi.Offset > 0 && pi.Length > 0)
        {
            TapeStatus.ThreadCount = BigEndianBitConverter.ToUInt32(_rawBytes, pi.Offset + 12);
            if (Manufacturer.Gen >= 4)
            {
                if ((BigEndianBitConverter.ToUInt64(_rawBytes, pi.Offset + 22) & 0xffff_ffff_ffff_0000) == 0xffff_ffff_ffff_0000)
                    TapeStatus.EncryptedData = false;
                else
                    TapeStatus.EncryptedData = true;
            }
            if (Manufacturer.IsCleaningTape)
                TapeStatus.LastLocation = BigEndianBitConverter.ToUInt16(_rawBytes, pi.Offset + 26);
        }
        if (Manufacturer.IsCleaningTape)
        {
            float cleanLength = 5.5f;
            if (TapeStatus.LastLocation >= 0 && Manufacturer.TapeLength >= 0)
            {
                int cleansRemaining = Manufacturer.TapeLength / 4 - 11;
                cleansRemaining -= TapeStatus.LastLocation / 4;
                cleansRemaining = (int)(cleansRemaining / cleanLength);
                if (cleansRemaining <= 0)
                    Manufacturer.IsCleanExpired = true;
            }
        }

        // Parse the initialisation page

        // Parse the EOD page for Partition 0-3
        int[] eodPages = [0x0104, 0x010e, 0x010f, 0x0110];
        for (int i = 0; i < 4; i++)
        {
            pi = _pages[eodPages[i]];
            if (pi.Offset > 0 && pi.Length > 0)
            {
                EODs[i] = new EOD()
                {
                    DataSet = BigEndianBitConverter.ToUInt32(_rawBytes, pi.Offset + 24),
                    WrapNumber = BigEndianBitConverter.ToUInt32(_rawBytes, pi.Offset + 28),
                    Validity = BigEndianBitConverter.ToUInt16(_rawBytes, pi.Offset + 32),
                    PhysicalPosition = BigEndianBitConverter.ToUInt32(_rawBytes, pi.Offset + 36),
                };
            }

        }

        // Parse the cartridge content page (if it exists!)

        // Parse the Tape Directory page

        // Parse the Suspended Append Writes page

        // Parse the Tape Control page (LTO9 Only)

        // Parse the Application Specific page

        Console.WriteLine(offset);
    }
}

public class TapeStatus
{
    public uint ThreadCount { get; set; }
    public bool EncryptedData { get; set; } = false;
    public ushort LastLocation { get; set; }
}

public class EOD
{
    public uint DataSet { get; set; }
    public uint WrapNumber { get; set; }
    public ushort Validity { get; set; }
    public uint PhysicalPosition { get; set; }
}
