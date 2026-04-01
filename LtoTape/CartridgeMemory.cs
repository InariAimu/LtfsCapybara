using System.Text;
using System.Text.RegularExpressions;

using LtoTape.CM;

namespace LtoTape;

public class CartridgeMemory
{
    private byte[] _rawBytes = [];

    private readonly Dictionary<int, PageInfo> _pages = [];

    public ApplicationSpecific ApplicationSpecific { get; set; } = new();
    public Manufacturer Manufacturer { get; set; } = new();
    public MediaManufacturer MediaManufacturer { get; set; } = new();
    public TapeStatus TapeStatus { get; set; } = new();
    public Dictionary<int, EOD> EODs { get; set; } = [];
    public Dictionary<int, PartitionInfo> Partitions { get; set; } = [];
    public Dictionary<int, Usage> Usages { get; set; } = [];
    public List<WrapInfo> Wraps { get; set; } = [];

    public Dictionary<int, UsagePage> UsagePages { get; set; } = [];

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

        Parse();
    }

    public void FromBinaryFile(string fileName)
    {
        _rawBytes = File.ReadAllBytes(fileName);
        Parse();
    }

    public void FromBytes(byte[] rawBytes)
    {
        _rawBytes = rawBytes is null ? [] : [.. rawBytes];
        Parse();
    }

    public void SaveToBinaryFile(string binOutputFile)
    {
        File.WriteAllBytes(binOutputFile, _rawBytes);
    }

    public void Parse()
    {
        if (_rawBytes.Length < 400)
            return;

        _pages.Clear();
        UsagePages.Clear();
        Usages.Clear();
        EODs.Clear();
        Partitions.Clear();
        Wraps.Clear();

        static bool IsValidPage(PageInfo p) => p.Offset > 0 && p.Length > 0;

        // Parse page directory
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
        if (_pages.TryGetValue(1, out PageInfo? pi) && pi.Length > 0)
        {
            Manufacturer.Parse(_rawBytes, pi.Offset);
        }

        // Parse the media manufacturers page
        if (_pages.TryGetValue(2, out pi) && pi.Length > 0)
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
        if (_pages.TryGetValue(0x0108, out pi) && pi.Length > 0)
            length = pi.Length;

        string mechRelatedInfoVendorID = "";
        bool hasMechInfo = _pages.TryGetValue(0x0106, out PageInfo? mechInfoPage) &&
                                                        IsValidPage(mechInfoPage);
        if (hasMechInfo && mechInfoPage is not null)
        {
            mechRelatedInfoVendorID =
                Encoding.ASCII.GetString(_rawBytes, mechInfoPage.Offset + 4, 8).TrimEnd();
        }

        List<UsagePage> usagePages = new(4);
        bool hasUsageError = false;
        for (int i = 0; i < 4; i++)
        {
            int key = i + 0x0108;

            if (_pages.TryGetValue(key, out pi) && IsValidPage(pi) && hasMechInfo && mechInfoPage is not null)
            {
                byte[] buff = new byte[length + 64];
                Array.Copy(_rawBytes, pi.Offset, buff, 0, length);
                Array.Copy(_rawBytes, mechInfoPage.Offset + 12 + 64 * i, buff, length, 64);

                usagePages.Add(new UsagePage()
                {
                    Data0 = buff,
                    Data1 = BigEndianBitConverter.ToUInt32(buff, offsets[0]),
                });
            }
            else
            {
                hasUsageError = true;
            }
        }

        if (!hasUsageError)
        {
            usagePages.Sort(static (a, b) => b.Data1.CompareTo(a.Data1));

            for (int i = 0; i < 4; i++)
            {
                UsagePages[i] = usagePages[i];
                UsagePages[i].Index = (uint)i;
            }

            for (int i = 0; i < 3; i++)
            {
                Usages[i] = new Usage();
                Usages[i].Parse(i, driveSNLength, offsets, UsagePages, Manufacturer, mechRelatedInfoVendorID, length);
            }
        }

        // Parse the "tape status and tape alert flags" page
        if (_pages.TryGetValue(0x0105, out pi) && IsValidPage(pi))
        {
            TapeStatus.Parse(_rawBytes, pi.Offset, Manufacturer.Gen, Manufacturer.IsCleaningTape);

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
        }

        // Parse the EOD page for Partition 0-3
        int[] eodPages = [0x0104, 0x010e, 0x010f, 0x0110];
        for (int i = 0; i < 4; i++)
        {
            if (!_pages.TryGetValue(eodPages[i], out pi))
                continue;

            if (IsValidPage(pi))
            {
                EODs[i] = new EOD();
                EODs[i].Parse(_rawBytes, pi.Offset);
            }
        }

        // Parse the Tape Directory page
        _pages.TryGetValue(0x103, out pi);
        int partitionCount = EODs.Count();
        if (pi is not null && IsValidPage(pi) && EODs.TryGetValue(0, out EOD? eod0) && eod0.Validity > 0)
        {
            int hdrLength = 16;
            if (Manufacturer.Gen >= 6)
                hdrLength = 48;

            uint lastId = 0;
            uint lastSetId = 0;

            var info = TapeInfo.GetPhysicInfo(Manufacturer.Gen);
            for (int index = 0; index < info.NWraps; index++)
            {
                WrapInfo w = new();
                int offset1 = pi.Offset + hdrLength + index * 32;
                w.Parse(_rawBytes, offset1, index, ref lastId, ref lastSetId, EODs, partitionCount, info);
                Wraps.Add(w);
            }
        }

        for (int i = 0; i < Wraps.Count; i++)
        {
            var w = Wraps[i];
            if (w.Type == WrapType.Empty || w.Type == WrapType.Guard)
            {
                w.StartBlock = 0;
                w.EndBlock = 0;
            }
            else
            {
                if (i == 0)
                {
                    w.StartBlock = 0;
                    w.EndBlock = (uint)(w.RecCount + w.FileMarkCount - 1);
                }
                else
                {
                    var prev = Wraps[i - 1];
                    w.StartBlock = (uint)(prev.EndBlock);
                    if (w.StartBlock != 0)
                        w.StartBlock += 1;

                    w.EndBlock = (uint)(w.StartBlock + w.RecCount + w.FileMarkCount - 1);
                }
            }

            if (w.Type == WrapType.EOD)
            {
                w.EndBlock += 1;
            }
        }

        // Build logical partitions from contiguous non-guard wraps.
        int setsPerWrap = Manufacturer.TapePhysicInfo.SetsPerWrap;
        long bytesPerSet = Manufacturer.TapePhysicInfo.KBytesPerSet * 1024L;

        PartitionInfo? currentPartition = null;
        bool stopLossCount = false;
        foreach (var wrap in Wraps)
        {
            if (wrap.Type == WrapType.Guard)
            {
                currentPartition = null;
                continue;
            }

            if (currentPartition is null)
            {
                int partitionId = Partitions.Count;
                currentPartition = new PartitionInfo
                {
                    Id = partitionId
                };
                Partitions[partitionId] = currentPartition;
                stopLossCount = false;
            }

            currentPartition.WrapCount += 1;

            long usedSets = Math.Max(0, (long)wrap.Set);
            long lossSets = 0;
            if (!stopLossCount)
            {
                if (wrap.Type == WrapType.EOD)
                {
                    stopLossCount = true;
                }
                else
                {
                    lossSets = Math.Max(0, (long)setsPerWrap - usedSets);
                }
            }

            currentPartition.UsedSize += usedSets * bytesPerSet;
            currentPartition.EstimatedLossSize += lossSets * bytesPerSet;
        }

        foreach (var partition in Partitions.Values)
        {
            partition.AllocatedSize = partition.WrapCount * (long)setsPerWrap * bytesPerSet;
        }

        // parse application specific page
        if (_pages.TryGetValue(0x0200, out pi) && IsValidPage(pi))
        {
            ApplicationSpecific.Parse(_rawBytes, pi.Offset, pi.Length);
        }
    }
}
