using System.Globalization;

namespace TapeDrive.Utils;

public static partial class StructParser
{
    private static StructMetadataField CreateMetadataField(object instance, StructFieldLayout layout, byte[] bytes)
    {
        var rawValue = GetMemberValue(instance, layout.Member);
        var rawBytes = new byte[layout.ByteLength];
        Array.Copy(bytes, layout.ByteIndex, rawBytes, 0, layout.ByteLength);

        var valueDescriptions = ParseValueDescriptions(layout.MetadataAttribute, rawValue);
        return new StructMetadataField
        {
            MemberName = layout.Member.Name,
            DisplayName = layout.MetadataAttribute?.Name ?? layout.Member.Name,
            DataType = layout.MemberType.Name,
            Encoding = layout.Encoding,
            Value = NormalizeValueForJson(rawValue),
            FormattedValue = FormatValue(rawValue),
            RawBytes = rawBytes.Select(static value => (int)value).ToArray(),
            RawHex = FormatHex(rawBytes),
            Description = layout.MetadataAttribute?.Description ?? string.Empty,
            MatchedValueDescription = valueDescriptions.FirstOrDefault(static item => item.IsCurrent)?.Description,
            ValueDescriptions = valueDescriptions,
            IsReserved = false,
            Location = new StructMetadataLocation
            {
                ByteIndex = layout.ByteIndex,
                EndByteIndex = layout.ByteIndex + layout.ByteLength - 1,
                ByteLength = layout.ByteLength,
                BitIndex = layout.BitIndex,
                BitLength = layout.BitLength,
            },
        };
    }

    private static object? NormalizeValueForJson(object? value)
    {
        return value switch
        {
            null => null,
            byte[] bytes => bytes.Select(static item => (int)item).ToArray(),
            sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal or bool or string => value,
            Enum enumValue => Convert.ChangeType(enumValue, Enum.GetUnderlyingType(enumValue.GetType()), CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            bool boolValue => boolValue ? "true" : "false",
            byte[] bytes => FormatHex(bytes),
            byte byteValue => $"{byteValue} (0x{byteValue:X2})",
            sbyte sbyteValue => $"{sbyteValue} (0x{unchecked((byte)sbyteValue):X2})",
            short shortValue => $"{shortValue} (0x{unchecked((ushort)shortValue):X4})",
            ushort ushortValue => $"{ushortValue} (0x{ushortValue:X4})",
            int intValue => $"{intValue} (0x{unchecked((uint)intValue):X8})",
            uint uintValue => $"{uintValue} (0x{uintValue:X8})",
            long longValue => $"{longValue} (0x{unchecked((ulong)longValue):X16})",
            ulong ulongValue => $"{ulongValue} (0x{ulongValue:X16})",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }

    private static StructMetadataValueDescription[] ParseValueDescriptions(MetadataAttribute? metadata, object? currentValue)
    {
        if (metadata?.ValueDescriptions == null || metadata.ValueDescriptions.Length == 0)
        {
            return [];
        }

        return metadata.ValueDescriptions.Select(entry =>
        {
            var (value, description) = SplitValueDescription(entry);
            return new StructMetadataValueDescription
            {
                Value = value,
                Description = description,
                IsCurrent = ValueMatches(currentValue, value),
            };
        }).ToArray();
    }

    private static (string Value, string Description) SplitValueDescription(string entry)
    {
        var normalized = entry.Replace("\r\n", "\n");
        var separatorIndex = normalized.IndexOf('\n');
        if (separatorIndex < 0)
        {
            return (normalized.Trim(), string.Empty);
        }

        return (
            normalized[..separatorIndex].Trim(),
            normalized[(separatorIndex + 1)..].Trim()
        );
    }

    private static bool ValueMatches(object? currentValue, string candidate)
    {
        if (currentValue == null)
        {
            return false;
        }

        if (currentValue is bool boolValue)
        {
            if (bool.TryParse(candidate, out var parsedBool))
            {
                return boolValue == parsedBool;
            }

            if (TryParseUnsignedInteger(candidate, out var parsedUnsigned))
            {
                return (boolValue ? 1UL : 0UL) == parsedUnsigned;
            }

            return false;
        }

        if (currentValue is byte[])
        {
            return false;
        }

        if (currentValue is Enum enumValue)
        {
            currentValue = Convert.ChangeType(enumValue, Enum.GetUnderlyingType(enumValue.GetType()), CultureInfo.InvariantCulture);
        }

        if (currentValue is sbyte or byte or short or ushort or int or uint or long or ulong)
        {
            if (TryParseUnsignedInteger(candidate, out var parsedUnsigned))
            {
                return Convert.ToUInt64(currentValue, CultureInfo.InvariantCulture) == parsedUnsigned;
            }

            return false;
        }

        return string.Equals(Convert.ToString(currentValue, CultureInfo.InvariantCulture), candidate, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseUnsignedInteger(string input, out ulong value)
    {
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(input[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        return ulong.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string FormatHex(byte[] bytes)
    {
        return string.Join(" ", bytes.Select(static value => value.ToString("X2", CultureInfo.InvariantCulture)));
    }
}