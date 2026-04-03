using TapeDrive;
using TapeDrive.SCSICommands;
using TapeDrive.SCSICommands.LogSensePages;
using TapeDrive.Utils;
using System.Text.Json;

namespace LtfsTest;

public class StructParserTest
{
    [Fact]
    public void Parse_MapsFixedFormatSenseDataFields()
    {
        byte[] sense =
        [
            0b1111_0000,
            0x00,
            0b1110_1010,
            0x12,
            0x34,
            0x56,
            0x78,
            0x10,
            0x9A,
            0xBC,
            0xDE,
            0xF0,
            0x5A,
            0xC3,
            0x7E,
            0b1100_0101,
            0x11,
            0x22,
            0x00,
            0x00,
            0x00,
            0b0000_1000,
        ];

        var parsed = StructParser.Parse<FixedFormatSenseData>(sense);

        Assert.True(parsed.Valid);
        Assert.Equal((byte)0x70, parsed.ErrorCode);
        Assert.True(parsed.Mark);
        Assert.True(parsed.EOM);
        Assert.True(parsed.ILI);
        Assert.Equal((byte)0x0A, parsed.SenseKey);
        Assert.Equal(new byte[] { 0x12, 0x34, 0x56, 0x78 }, parsed.InformationBytes);
        Assert.Equal((byte)0x10, parsed.AdditionalSenseLength);
        Assert.Equal(new byte[] { 0x9A, 0xBC, 0xDE, 0xF0 }, parsed.CommandSpecificInformationBytes);
        Assert.Equal((byte)0x5A, parsed.AdditionalSenseCode);
        Assert.Equal((byte)0xC3, parsed.AdditionalSenseCodeQualifier);
        Assert.Equal((byte)0x7E, parsed.FieldReplaceableUnitCode);
        Assert.True(parsed.SKSV);
        Assert.True(parsed.CPE);
        Assert.False(parsed.BPV);
        Assert.Equal((byte)0x05, parsed.BitPointer);
        Assert.Equal((ushort)0x1122, parsed.FieldPointerOrDriveErrorCode);
        Assert.True(parsed.CLN);
    }

    [Fact]
    public void Parse_SupportsDWordAttribute()
    {
        byte[] bytes = [0x12, 0x34, 0x56, 0x78];

        var parsed = StructParser.Parse<TestDWordStruct>(bytes);

        Assert.Equal((uint)0x12345678, parsed.Value);
    }

    [Fact]
    public void ToBytes_SerializesFixedFormatSenseData()
    {
        var sense = new FixedFormatSenseData
        {
            Valid = true,
            ErrorCode = 0x70,
            Mark = true,
            EOM = true,
            ILI = true,
            SenseKey = 0x0A,
            InformationBytes = [0x12, 0x34, 0x56, 0x78],
            AdditionalSenseLength = 0x10,
            CommandSpecificInformationBytes = [0x9A, 0xBC, 0xDE, 0xF0],
            AdditionalSenseCode = 0x5A,
            AdditionalSenseCodeQualifier = 0xC3,
            FieldReplaceableUnitCode = 0x7E,
            SKSV = true,
            CPE = true,
            BPV = false,
            BitPointer = 0x05,
            FieldPointerOrDriveErrorCode = 0x1122,
            CLN = true,
        };

        var bytes = StructParser.ToBytes(sense);

        Assert.Equal(
            new byte[]
            {
                0b1111_0000,
                0x00,
                0b1110_1010,
                0x12,
                0x34,
                0x56,
                0x78,
                0x10,
                0x9A,
                0xBC,
                0xDE,
                0xF0,
                0x5A,
                0xC3,
                0x7E,
                0b1100_0101,
                0x11,
                0x22,
                0x00,
                0x00,
                0x00,
                0b0000_1000,
                0x00,
                0x00,
            },
            bytes
        );
    }

    [Fact]
    public void ToMetadataDocument_ExportsLayoutAndDescriptions()
    {
        var sense = StructParser.Parse<FixedFormatSenseData>(
        [
            0b1111_0000,
            0x00,
            0b1110_1010,
            0x12,
            0x34,
            0x56,
            0x78,
            0x10,
            0x9A,
            0xBC,
            0xDE,
            0xF0,
            0x5A,
            0xC3,
            0x7E,
            0b1100_0101,
            0x11,
            0x22,
            0x00,
            0x00,
            0x00,
            0b0000_1000,
        ]);

        var document = StructParser.ToMetadataDocument(sense);

        Assert.Equal(nameof(FixedFormatSenseData), document.TypeName);
        Assert.Equal(24, document.ByteLength);
        Assert.Equal("F0 00 EA 12 34 56 78 10 9A BC DE F0 5A C3 7E C5 11 22 00 00 00 08 00 00", document.RawHex);

        var validField = Assert.Single(document.Fields.Where(field => field.MemberName == nameof(FixedFormatSenseData.Valid)));
        Assert.Equal("Valid", validField.DisplayName);
        Assert.Equal("bit", validField.Encoding);
        Assert.Equal(0, validField.Location.ByteIndex);
        Assert.Equal(7, validField.Location.BitIndex);
        Assert.Equal(1, validField.Location.BitLength);
        Assert.Equal(true, validField.Value);
        Assert.Equal("true", validField.FormattedValue);
        Assert.Equal("1", Assert.Single(validField.ValueDescriptions).Value);

        var errorCodeField = Assert.Single(document.Fields.Where(field => field.MemberName == nameof(FixedFormatSenseData.ErrorCode)));
        Assert.Equal("112 (0x70)", errorCodeField.FormattedValue);
        Assert.Equal("Indicates that the error is current, that is, it is associated with the command for which CHECK CONDITION status has been reported.", errorCodeField.MatchedValueDescription);
        Assert.Equal(2, errorCodeField.ValueDescriptions.Length);
        Assert.True(errorCodeField.ValueDescriptions[0].IsCurrent);
    }

    [Fact]
    public void ToMetadataJson_UsesCamelCasePropertyNames()
    {
        var json = StructParser.ToMetadataJson(new TestDWordStruct { Value = 0x12345678 });

        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty("typeName", out _));
        Assert.True(document.RootElement.TryGetProperty("fields", out _));
    }

    [Fact]
    public void Parse_SupportsByteListAttribute()
    {
        byte[] bytes = [0x2F, 0x00, 0x00, 0x03, 0x00, 0x02, 0x17];

        var parsed = StructParser.Parse<LogSenseSupportedPage>(bytes);

        Assert.Equal((byte)0x2F, parsed.PageCode);
        Assert.Equal(new byte[] { 0x00, 0x02, 0x17 }, parsed.SupportedPageCodes);
    }

    [Fact]
    public void ToBytes_SupportsByteListAttribute()
    {
        var page = new LogSenseSupportedPage
        {
            PageCode = 0x2F,
            SupportedPageCodes = [0x00, 0x02, 0x17],
        };

        var bytes = StructParser.ToBytes(page);

        Assert.Equal(new byte[] { 0x2F, 0x00, 0x00, 0x03, 0x00, 0x02, 0x17 }, bytes);
    }

    [Fact]
    public void ToMetadataDocument_PreservesExplicitLengthAndByteListFields()
    {
        var payload = new TestVariableStruct
        {
            Header = 0x12,
            Payload = [0xAA, 0xBB, 0xCC],
        };

        var document = StructParser.ToMetadataDocument(payload);

        Assert.Equal(8, document.ByteLength);
        Assert.Equal("Variable test struct", document.Description);

        var payloadField = Assert.Single(document.Fields.Where(field => field.MemberName == nameof(TestVariableStruct.Payload)));
        Assert.Equal("byteList", payloadField.Encoding);
        Assert.Equal(1, payloadField.Location.ByteIndex);
        Assert.Equal(4, payloadField.Location.ByteLength);
        Assert.Equal("AA BB CC", payloadField.FormattedValue);
        Assert.NotNull(payloadField.ListLayout);
        Assert.Equal("prefix", payloadField.ListLayout!.LengthSource);
        Assert.Equal("byte", payloadField.ListLayout.LengthEncoding);
        Assert.Equal(1, payloadField.ListLayout.LengthByteIndex);
        Assert.Equal(2, payloadField.ListLayout.ValueByteIndex);
        Assert.Equal(3, payloadField.ListLayout.ValueByteLength);
        Assert.DoesNotContain(document.Fields, field => field.IsReserved);
    }

    [Fact]
    public void Parse_SupportsRefByteListAttribute()
    {
        byte[] bytes =
        [
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x12, 0x34,
            0x00,
            0x02,
            0x5A,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x03,
            0x00, 0x00, 0x00, 0x02,
            0xDE, 0xAD,
            0x70, 0x00, 0x05,
        ];

        var parsed = StructParser.Parse<SenseResponse>(bytes);

        Assert.Equal((ushort)0x1234, parsed.StatusQualifier);
        Assert.Equal((byte)0x02, parsed.DATAPRES);
        Assert.Equal((byte)0x5A, parsed.Status);
        Assert.Equal((uint)3, parsed.SenseDataLength);
        Assert.Equal((uint)2, parsed.ResponseDataLength);
        Assert.Equal(new byte[] { 0xDE, 0xAD }, parsed.ResponseData);
        Assert.Equal(new byte[] { 0x70, 0x00, 0x05 }, parsed.SenseData);
    }

    [Fact]
    public void ToBytes_SynchronizesReferencedByteListLengths()
    {
        var response = new SenseResponse
        {
            StatusQualifier = 0x1234,
            DATAPRES = 0x02,
            Status = 0x5A,
            ResponseData = [0xDE, 0xAD],
            SenseData = [0x70, 0x00, 0x05],
        };

        var bytes = StructParser.ToBytes(response);

        Assert.Equal((uint)2, response.ResponseDataLength);
        Assert.Equal((uint)3, response.SenseDataLength);
        Assert.Equal(
            new byte[]
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x12, 0x34,
                0x00,
                0x02,
                0x5A,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x03,
                0x00, 0x00, 0x00, 0x02,
                0xDE, 0xAD,
                0x70, 0x00, 0x05,
            },
            bytes
        );
    }

    [Fact]
    public void ToMetadataDocument_ExportsReferencedByteListLayout()
    {
        var response = new SenseResponse
        {
            StatusQualifier = 0x1234,
            DATAPRES = 0x02,
            Status = 0x5A,
            ResponseData = [0xDE, 0xAD],
            SenseData = [0x70, 0x00, 0x05],
        };

        var document = StructParser.ToMetadataDocument(response);

        var responseDataField = Assert.Single(document.Fields.Where(field => field.MemberName == nameof(SenseResponse.ResponseData)));
        Assert.Equal("refByteList", responseDataField.Encoding);
        Assert.Equal("DE AD", responseDataField.FormattedValue);
        Assert.NotNull(responseDataField.ListLayout);
        Assert.Equal("member", responseDataField.ListLayout!.LengthSource);
        Assert.Equal("dword", responseDataField.ListLayout.LengthEncoding);
        Assert.Equal(nameof(SenseResponse.ResponseDataLength), responseDataField.ListLayout.LengthFieldMemberName);
        Assert.Equal(20, responseDataField.ListLayout.LengthByteIndex);
        Assert.Equal(24, responseDataField.ListLayout.ValueByteIndex);
        Assert.Equal(2, responseDataField.ListLayout.ValueByteLength);

        var senseDataField = Assert.Single(document.Fields.Where(field => field.MemberName == nameof(SenseResponse.SenseData)));
        Assert.Equal(26, senseDataField.Location.ByteIndex);
        Assert.Equal(3, senseDataField.Location.ByteLength);
        Assert.NotNull(senseDataField.ListLayout);
        Assert.Equal(nameof(SenseResponse.SenseDataLength), senseDataField.ListLayout!.LengthFieldMemberName);
        Assert.Equal(16, senseDataField.ListLayout.LengthByteIndex);
        Assert.Equal(26, senseDataField.ListLayout.ValueByteIndex);
        Assert.Equal(3, senseDataField.ListLayout.ValueByteLength);
    }

    [MSBFirstStruct]
    private class TestDWordStruct
    {
        [DWord(0)]
        public uint Value { get; set; }
    }

    [MSBFirstStruct("Variable test struct", ExplicitByteLength = 8)]
    private class TestVariableStruct
    {
        [Byte(0)]
        public byte Header { get; set; }

        [ByteList(-1, LengthType.Byte)]
        public byte[] Payload { get; set; } = [];
    }
}