namespace TapeDrive.Utils;

[AttributeUsage(AttributeTargets.Class)]
public class MSBFirstStructAttribute(
    string description = "",
    int explicitByteLength = 0,
    bool emptySpaceAsReserved = true,
    bool reservedSpacesFillZero = true) : Attribute
{
    public string Description { get; init; } = description;

    public int ExplicitByteLength { get; init; } = explicitByteLength;

    public bool EmptySpaceAsReserved { get; init; } = emptySpaceAsReserved;
    public bool ReservedSpacesFillZero { get; init; } = reservedSpacesFillZero;
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class BitAttribute(int byteIndex, int bitIndex) : Attribute
{
    public int ByteIndex { get; init; } = byteIndex;
    public int BitIndex { get; init; } = bitIndex;
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ByteAttribute(int byteIndex, int bitIndex = 0, int length = 8) : Attribute
{
    public int ByteIndex { get; init; } = byteIndex;
    public int BitIndex { get; init; } = bitIndex;
    public int BitLength { get; init; } = length;
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class BytesAttribute(int byteIndex, int length) : Attribute
{
    public int ByteIndex { get; init; } = byteIndex;
    public int Length { get; init; } = length;
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class WordAttribute(int byteIndex) : Attribute
{
    public int ByteIndex { get; init; } = byteIndex;
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class DWordAttribute(int byteIndex) : Attribute
{
    public int ByteIndex { get; init; } = byteIndex;
}



[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class RefByteListAttribute(int byteIndex, string refField) : Attribute
{
    /// <summary>
    /// -1 means the byte index is automatically determined based on the end of the previous field
    /// </summary>
    public int ByteIndex { get; init; } = byteIndex;

    /// <summary>
    /// name of the field/property that indicates the length of the list
    /// </summary>
    public string RefField { get; init; } = refField;
}

public enum LengthType
{
    Byte, Word, Dword
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ByteListAttribute(int byteIndex, LengthType lengthType, bool isLengthMSBFirst = true) : Attribute
{
    /// <summary>
    /// -1 means the byte index is automatically determined based on the end of the previous field
    /// </summary>
    public int ByteIndex { get; init; } = byteIndex;

    /// <summary>
    /// first byte/word/dword indicates the length of the list, followed by that many bytes of data
    /// </summary>
    public LengthType LengthType { get; init; } = lengthType;

    public bool IsLengthMSBFirst { get; init; } = isLengthMSBFirst;
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class MetadataAttribute(string name, string description, string[]? valueDescriptions = null) : Attribute
{
    public string Name { get; init; } = name;
    public string Description { get; init; } = description;
    public string[]? ValueDescriptions { get; init; } = valueDescriptions;
}
