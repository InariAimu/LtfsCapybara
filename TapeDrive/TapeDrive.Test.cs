using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using LtoTape;

namespace TapeDrive;

/// <summary>
/// Test double for tape drive operations. Methods return success/neutral
/// values so unit tests can exercise higher-level logic without hardware.
/// </summary>
public class FakeTapeDrive : TapeDriveBase
{
    private sealed class FakeRecord
    {
        public required bool IsFileMark { get; init; }
        public required byte[] Data { get; init; }
    }

    private readonly Dictionary<byte, List<FakeRecord>> _partitions = new()
    {
        [0] = [],
        [1] = [],
    };

    private readonly Dictionary<ushort, byte[]> _mamAttributes = new();

    private byte _currentPartition;
    private int _currentRecordIndex;
    private ulong _configuredCapacity = 0xffff;
    private ulong _configuredBlockSize;
    private string _barcode;

    public FakeTapeDrive()
    {
        Sense = new byte[64];
        _barcode = "TEST001L6";
        SeedMamAttributes();
    }

    public override bool IOCtlDirect(byte[] cdb, IntPtr dataBuffer, uint bufferLength, byte dataIn = 0, uint timeoutSeconds = 600)
    {
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
        if (cdb.Length > 0 && cdb[0] == 0x04)
        {
            ResetTapeContent();
        }

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

    public override bool TestUnitReady() => true;

    public override bool GetInquiry() => true;

    public override void Load()
    {
        _currentPartition = 0;
        _currentRecordIndex = 0;
    }

    public override void LoadUnthread()
    {
        _currentPartition = 0;
        _currentRecordIndex = 0;
    }

    public override void Unload()
    {
        _currentPartition = 0;
        _currentRecordIndex = 0;
    }

    public override void Unthread()
    {
        _currentPartition = 0;
        _currentRecordIndex = 0;
    }

    public override bool Rewind()
    {
        _currentPartition = 0;
        _currentRecordIndex = 0;
        return true;
    }

    public override byte[] ReadDiagCM(int len10h = 0)
    {
        return [];
    }

    public override string ReadBarCode() => _barcode;

    public override string ReadAppInfo()
    {
        var vendor = GetMAMAttribute(0x0800).DataAsString.TrimEnd();
        var app = GetMAMAttribute(0x0801).DataAsString.TrimEnd();
        var version = GetMAMAttribute(0x0802).DataAsString.TrimEnd();
        return string.Join(" ", new[] { vendor, app, version }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    public override ulong ReadRemainingCapacity() => _configuredCapacity;

    public override byte ReadDensityCode() => 0x5a;

    public override bool SetBarcode(string barcode)
    {
        _barcode = (barcode ?? string.Empty).Trim().ToUpperInvariant();
        _mamAttributes[0x0806] = Encoding.ASCII.GetBytes(_barcode.PadRight(32));
        return true;
    }

    public override bool SetBlockSize(ulong blockSize)
    {
        _configuredBlockSize = blockSize;
        return true;
    }

    public override bool WriteFileMarks(uint number, bool immed = true)
    {
        if (number == 0)
            return true;

        PrepareForWrite();
        var partition = GetCurrentPartition();
        for (var i = 0; i < number; i++)
        {
            partition.Add(new FakeRecord { IsFileMark = true, Data = [] });
            _currentRecordIndex += 1;
        }

        return true;
    }

    public override bool Flush() => true;

    public override bool SetCapacity(ushort capacity)
    {
        _configuredCapacity = capacity;
        return true;
    }

    public override bool InitTape()
    {
        ResetTapeContent();
        return true;
    }

    public override bool SelectPartitionMode(byte[] modeData, byte maxPartitions, ushort p0size, ushort p1size) => true;

    public override byte[] ModeSense(byte pageID, bool skipHeader = true)
    {
        return [0x00, 0x00, 0x02, 0x01];
    }

    public override BlockLimit ReadBlockLimit() => new() { MaxBlockLength = 1024 * 1024, MinBlockLength = 1 };

    public override bool SetEncryption(byte[]? encryptionKey = null) => true;

    public override ushort Locate(ulong blockAddress, byte partitionNumber, LocateType locateType)
    {
        _currentPartition = partitionNumber;
        var partition = GetCurrentPartition();

        _currentRecordIndex = locateType switch
        {
            LocateType.Block => FindDataRecordIndex(partition, blockAddress),
            LocateType.FileMark => FindFileMarkIndex(partition, blockAddress),
            LocateType.EOD => partition.Count,
            _ => partition.Count,
        };

        return 0;
    }

    public override byte[] ReadBlock(uint blockSizeLimit = 0x080000, bool truncate = false)
    {
        var partition = GetCurrentPartition();
        if (_currentRecordIndex >= partition.Count)
            throw new InvalidOperationException("No block exists at the current position.");

        var record = partition[_currentRecordIndex];
        if (record.IsFileMark)
            throw new InvalidOperationException("Current position points to a filemark, not a data block.");

        _currentRecordIndex += 1;
        if (blockSizeLimit > 0 && truncate && record.Data.Length > blockSizeLimit)
            return record.Data[..(int)blockSizeLimit];

        return [.. record.Data];
    }

    public override PositionData ReadPosition()
    {
        var partition = GetCurrentPartition();
        var boundedIndex = Math.Min(_currentRecordIndex, partition.Count);
        ulong blockNumber = 0;
        ulong fileNumber = 0;

        for (var i = 0; i < boundedIndex; i++)
        {
            if (partition[i].IsFileMark)
                fileNumber += 1;
            else
                blockNumber += 1;
        }

        return new PositionData
        {
            BOP = boundedIndex == 0,
            EOP = boundedIndex >= partition.Count,
            PartitionNumber = _currentPartition,
            BlockNumber = blockNumber,
            FileNumber = fileNumber,
            AddSenseKey = boundedIndex >= partition.Count ? (ushort)5 : (ushort)0,
        };
    }

    public override byte[] ReadToFileMark(int blockSizeLimit = 0x080000)
    {
        var partition = GetCurrentPartition();
        if (_currentRecordIndex >= partition.Count)
            return [];

        var bytes = new List<byte>();
        while (_currentRecordIndex < partition.Count && !partition[_currentRecordIndex].IsFileMark)
        {
            bytes.AddRange(partition[_currentRecordIndex].Data);
            _currentRecordIndex += 1;
        }

        if (blockSizeLimit > 0 && bytes.Count > blockSizeLimit)
            return bytes.Take(blockSizeLimit).ToArray();

        return bytes.ToArray();
    }

    public override bool ReadFileMark()
    {
        var partition = GetCurrentPartition();
        if (_currentRecordIndex < partition.Count && partition[_currentRecordIndex].IsFileMark)
        {
            _currentRecordIndex += 1;
            return true;
        }

        return false;
    }

    public override bool Write(byte[] data) => Write(data, (int)_configuredBlockSize);

    public override bool Write(byte[] data, int blockSize)
    {
        PrepareForWrite();
        GetCurrentPartition().Add(new FakeRecord { IsFileMark = false, Data = [.. data] });
        _currentRecordIndex += 1;
        return true;
    }

    public override bool PreAllocWriteBuffer(int blockSize) => true;

    public override void ReleaseWriteBuffer() { }

    public override bool BufferedWrite(ReadOnlyMemory<byte> data, int blockSize)
    {
        return Write(data.ToArray(), blockSize);
    }

    public override MAMAttribute GetMAMAttribute(ushort PageCode, byte PartitionNumber = 0)
    {
        if (_mamAttributes.TryGetValue(PageCode, out var raw))
        {
            return new MAMAttribute { ID = PageCode, RawData = [.. raw] };
        }

        if (PageCode == 0x0009)
        {
            return new MAMAttribute { ID = PageCode, RawData = [0, 0, 0, 0] };
        }

        if (PageCode == 0x0806)
        {
            return new MAMAttribute { ID = PageCode, RawData = Encoding.ASCII.GetBytes(_barcode.PadRight(32)) };
        }

        return new MAMAttribute { ID = PageCode, RawData = [] };
    }

    public override bool SetMAMAttribute(ushort pageID, string text, int textMaxLength, byte partition = 0)
    {
        var normalizedText = text ?? string.Empty;
        var value = normalizedText.PadRight(textMaxLength);
        _mamAttributes[pageID] = Encoding.ASCII.GetBytes(value);
        if (pageID == 0x0806)
        {
            _barcode = normalizedText.Trim().ToUpperInvariant();
        }

        return true;
    }

    public override bool SetMAMAttribute(ushort pageID, byte[] data, AttributeFormat format = AttributeFormat.Binary, byte partition = 0)
    {
        _mamAttributes[pageID] = data is null ? [] : [.. data];
        return true;
    }

    private void SeedMamAttributes()
    {
        _mamAttributes[0x0800] = Encoding.ASCII.GetBytes("capybara".PadRight(8));
        _mamAttributes[0x0801] = Encoding.ASCII.GetBytes("LTFS capybara".PadRight(16));
        _mamAttributes[0x0802] = Encoding.ASCII.GetBytes("0.0.1".PadRight(8));
        _mamAttributes[0x0806] = Encoding.ASCII.GetBytes(_barcode.PadRight(32));
        _mamAttributes[0x0009] = [0, 0, 0, 0];
    }

    private void ResetTapeContent()
    {
        _partitions[0].Clear();
        _partitions[1].Clear();
        _currentPartition = 0;
        _currentRecordIndex = 0;
    }

    private List<FakeRecord> GetCurrentPartition()
    {
        if (!_partitions.TryGetValue(_currentPartition, out var partition))
        {
            partition = [];
            _partitions[_currentPartition] = partition;
        }

        return partition;
    }

    private void PrepareForWrite()
    {
        var partition = GetCurrentPartition();
        if (_currentRecordIndex < partition.Count)
        {
            partition.RemoveRange(_currentRecordIndex, partition.Count - _currentRecordIndex);
        }
    }

    private static int FindDataRecordIndex(List<FakeRecord> partition, ulong blockAddress)
    {
        ulong currentBlock = 0;
        for (var i = 0; i < partition.Count; i++)
        {
            if (partition[i].IsFileMark)
                continue;

            if (currentBlock == blockAddress)
                return i;

            currentBlock += 1;
        }

        return partition.Count;
    }

    private static int FindFileMarkIndex(List<FakeRecord> partition, ulong fileMarkNumber)
    {
        if (fileMarkNumber == 0)
            return partition.Count;

        ulong currentFileMark = 0;
        for (var i = 0; i < partition.Count; i++)
        {
            if (!partition[i].IsFileMark)
                continue;

            currentFileMark += 1;
            if (currentFileMark == fileMarkNumber)
                return i;
        }

        return partition.Count;
    }
}
