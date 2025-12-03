using System.Xml.Serialization;

namespace Ltfs.Label;

/// <summary>
/// Represents a 'partitionid' element.
/// Restricted to a single lowercase letter [a-z].
/// </summary>
public class PartitionId
{
    private string _value = "a";

    /// <summary>
    /// Gets or sets the value of the 'partitionid'.
    /// Must be a single lowercase letter.
    /// </summary>
    [XmlText]
    public string Value
    {
        get => _value;
        set
        {
            if (string.IsNullOrEmpty(value) || value.Length != 1 || !char.IsLower(value[0]))
                throw new ArgumentException("PartitionID must be a single lowercase letter.");
            _value = value;
        }
    }

    /// <summary>
    /// Implicit conversion from string to PartitionId.
    /// </summary>
    /// <param name="value">The string value to convert.</param>
    public static implicit operator PartitionId(string value)
    {
        return new PartitionId { Value = value };
    }

    /// <summary>
    /// Implicit conversion from PartitionId to string.
    /// </summary>
    /// <param name="partitionId">The PartitionId to convert.</param>
    public static implicit operator string(PartitionId partitionId)
    {
        return partitionId?._value;
    }

    /// <summary>
    /// Returns the string representation of the PartitionId.
    /// </summary>
    /// <returns>The string value of the PartitionId.</returns>
    public override string ToString()
    {
        return Value;
    }
}