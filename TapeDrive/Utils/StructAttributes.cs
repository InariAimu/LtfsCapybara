[AttributeUsage(AttributeTargets.Class)]
public class MSBFirstStructAttribute : Attribute
{
    public string Description { get; init; }

    public int ExplicitByteLength { get; init; }

    public bool EmptySpaceAsReserved { get; init; }
    public bool ReservedSpacesFillZero { get; init; }

    public MSBFirstStructAttribute(string description = "", int explicitByteLength = 0, bool emptySpaceAsReserved = true, bool reservedSpacesFillZero = true)
    {
        Description = description;
        ExplicitByteLength = explicitByteLength;
        EmptySpaceAsReserved = emptySpaceAsReserved;
        ReservedSpacesFillZero = reservedSpacesFillZero;
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class BitAttribute : Attribute
{
    public int ByteIndex { get; init; }
    public int BitIndex { get; init; }

    public BitAttribute(int byteIndex, int bitIndex)
    {
        ByteIndex = byteIndex;
        BitIndex = bitIndex;
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ByteAttribute : Attribute
{
    public int ByteIndex { get; init; }
    public int BitIndex { get; init; }
    public int BitLength { get; init; }

    public ByteAttribute(int byteIndex, int bitIndex = 0, int length = 8)
    {
        ByteIndex = byteIndex;
        BitIndex = bitIndex;
        BitLength = length;
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class BytesAttribute : Attribute
{
    public int ByteIndex { get; init; }
    public int Length { get; init; }

    public BytesAttribute(int byteIndex, int length)
    {
        ByteIndex = byteIndex;
        Length = length;
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class WordAttribute : Attribute
{
    public int ByteIndex { get; init; }

    public WordAttribute(int byteIndex)
    {
        ByteIndex = byteIndex;
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class DWordAttribute : Attribute
{
    public int ByteIndex { get; init; }

    public DWordAttribute(int byteIndex)
    {
        ByteIndex = byteIndex;
    }
}


public enum LengthType
{
    Byte, Word, Dword
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ByteListAttribute : Attribute
{
    /// <summary>
    /// -1 means the byte index is automatically determined based on the end of the previous field
    /// </summary>
    public int ByteIndex { get; init; }
    
    /// <summary>
    /// first byte/word/dword indicates the length of the list, followed by that many bytes of data
    /// </summary>
    public LengthType LengthType { get; init; }

    public bool IsLengthMSBFirst { get; init; } = true;

    public ByteListAttribute(int byteIndex, LengthType lengthType, bool isLengthMSBFirst = true)
    {
        ByteIndex = byteIndex;
        LengthType = lengthType;
        IsLengthMSBFirst = isLengthMSBFirst;
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class MetadataAttribute : Attribute
{
    public string Name { get; init; }
    public string Description { get; init; }
    public string[]? ValueDescriptions { get; init; }

    public MetadataAttribute(string name, string description, string[]? valueDescriptions = null)
    {
        Name = name;
        Description = description;
        ValueDescriptions = valueDescriptions;
    }
}