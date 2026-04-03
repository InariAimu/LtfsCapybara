namespace TapeDrive.Utils;

public class StructMetadataDocument
{
    public string TypeName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int ByteLength { get; init; }
    public int[] RawBytes { get; init; } = [];
    public string RawHex { get; init; } = string.Empty;
    public StructMetadataField[] Fields { get; init; } = [];
}

public class StructMetadataField
{
    public string MemberName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string DataType { get; init; } = string.Empty;
    public string Encoding { get; init; } = string.Empty;
    public object? Value { get; init; }
    public string FormattedValue { get; init; } = string.Empty;
    public int[] RawBytes { get; init; } = [];
    public string RawHex { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? MatchedValueDescription { get; init; }
    public StructMetadataValueDescription[] ValueDescriptions { get; init; } = [];
    public bool IsReserved { get; init; }
    public StructMetadataLocation Location { get; init; } = new();
    public StructMetadataListLayout? ListLayout { get; init; }
}

public class StructMetadataLocation
{
    public int ByteIndex { get; init; }
    public int EndByteIndex { get; init; }
    public int ByteLength { get; init; }
    public int? BitIndex { get; init; }
    public int? BitLength { get; init; }
}

public class StructMetadataValueDescription
{
    public string Value { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsCurrent { get; init; }
}

public class StructMetadataListLayout
{
    public string LengthSource { get; init; } = string.Empty;
    public string LengthEncoding { get; init; } = string.Empty;
    public string? LengthFieldMemberName { get; init; }
    public int LengthByteIndex { get; init; }
    public int LengthEndByteIndex { get; init; }
    public int LengthByteLength { get; init; }
    public bool? IsLengthMSBFirst { get; init; }
    public int ValueByteIndex { get; init; }
    public int ValueEndByteIndex { get; init; }
    public int ValueByteLength { get; init; }
}