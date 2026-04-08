using System.Globalization;
using System.Reflection;

using LtoTape;

namespace TapeDrive.Utils;

public static partial class StructParser
{
    private static IEnumerable<MemberInfo> GetAnnotatedMembers(Type type)
    {
        return type
            .GetMembers(MemberBindingFlags)
            .Where(member => member is FieldInfo or PropertyInfo)
            .Where(member =>
                member.GetCustomAttribute<BitAttribute>() != null ||
                member.GetCustomAttribute<ByteAttribute>() != null ||
                member.GetCustomAttribute<BytesAttribute>() != null ||
                member.GetCustomAttribute<WordAttribute>() != null ||
                member.GetCustomAttribute<DWordAttribute>() != null ||
                member.GetCustomAttribute<RefByteListAttribute>() != null ||
                member.GetCustomAttribute<ByteListAttribute>() != null)
            .OrderBy(member => member.MetadataToken);
    }

    private static object ReadAnnotatedValue(StructFieldLayout layout, byte[] data)
    {
        if (layout.BitAttribute is BitAttribute bitAttribute)
        {
            EnsureByteAvailable(data, bitAttribute.ByteIndex);
            var bitValue = (data[bitAttribute.ByteIndex] >> bitAttribute.BitIndex) & 0x01;
            return ConvertNumericValue(layout.MemberType, bitValue);
        }

        if (layout.ByteAttribute is ByteAttribute byteAttribute)
        {
            EnsureByteAvailable(data, byteAttribute.ByteIndex);
            ValidateBitWindow(byteAttribute.BitIndex, byteAttribute.BitLength);

            var mask = byteAttribute.BitLength == 8
                ? 0xFF
                : (1 << byteAttribute.BitLength) - 1;
            var byteValue = (data[byteAttribute.ByteIndex] >> byteAttribute.BitIndex) & mask;
            return ConvertNumericValue(layout.MemberType, byteValue);
        }

        if (layout.BytesAttribute is BytesAttribute bytesAttribute)
        {
            EnsureRangeAvailable(data, bytesAttribute.ByteIndex, bytesAttribute.Length);
            var buffer = new byte[bytesAttribute.Length];
            Array.Copy(data, bytesAttribute.ByteIndex, buffer, 0, bytesAttribute.Length);
            if (layout.MemberType != typeof(byte[]))
            {
                throw new InvalidOperationException($"Member {layout.Member.Name} must be of type byte[] when using {nameof(BytesAttribute)}.");
            }

            return buffer;
        }

        if (layout.ByteListAttribute is ByteListAttribute)
        {
            if (layout.MemberType != typeof(byte[]))
            {
                throw new InvalidOperationException($"Member {layout.Member.Name} must be of type byte[] when using {nameof(ByteListAttribute)}.");
            }

            EnsureRangeAvailable(data, layout.ValueByteIndex, layout.ValueByteLength);
            var buffer = new byte[layout.ValueByteLength];
            Array.Copy(data, layout.ValueByteIndex, buffer, 0, layout.ValueByteLength);
            return buffer;
        }

        if (layout.RefByteListAttribute is RefByteListAttribute)
        {
            if (layout.MemberType != typeof(byte[]))
            {
                throw new InvalidOperationException($"Member {layout.Member.Name} must be of type byte[] when using {nameof(RefByteListAttribute)}.");
            }

            EnsureRangeAvailable(data, layout.ValueByteIndex, layout.ValueByteLength);
            var buffer = new byte[layout.ValueByteLength];
            Array.Copy(data, layout.ValueByteIndex, buffer, 0, layout.ValueByteLength);
            return buffer;
        }

        if (layout.WordAttribute is WordAttribute wordAttribute)
        {
            EnsureRangeAvailable(data, wordAttribute.ByteIndex, sizeof(ushort));
            return ReadWordValue(layout.MemberType, data, wordAttribute.ByteIndex);
        }

        if (layout.DWordAttribute is DWordAttribute dwordAttribute)
        {
            ValidateDWordLength(dwordAttribute.Length);
            EnsureRangeAvailable(data, dwordAttribute.ByteIndex, dwordAttribute.Length);
            return ReadDWordValue(layout.MemberType, data, dwordAttribute.ByteIndex, dwordAttribute.Length);
        }

        throw new InvalidOperationException($"Member {layout.Member.Name} does not have a supported parser attribute.");
    }

    private static void WriteAnnotatedValue(StructFieldLayout layout, byte[] data, object? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (layout.BitAttribute is BitAttribute bitAttribute)
        {
            EnsureByteAvailable(data, bitAttribute.ByteIndex);
            var bitMask = 1 << bitAttribute.BitIndex;
            if (ConvertToBoolean(value))
            {
                data[bitAttribute.ByteIndex] = (byte)(data[bitAttribute.ByteIndex] | bitMask);
            }
            else
            {
                data[bitAttribute.ByteIndex] = (byte)(data[bitAttribute.ByteIndex] & ~bitMask);
            }

            return;
        }

        if (layout.ByteAttribute is ByteAttribute byteAttribute)
        {
            EnsureByteAvailable(data, byteAttribute.ByteIndex);
            ValidateBitWindow(byteAttribute.BitIndex, byteAttribute.BitLength);

            var mask = byteAttribute.BitLength == 8
                ? 0xFF
                : (1 << byteAttribute.BitLength) - 1;
            var numericValue = checked((int)Convert.ToUInt64(value, CultureInfo.InvariantCulture));
            if ((numericValue & ~mask) != 0)
            {
                throw new ArgumentOutOfRangeException(layout.Member.Name, value, $"Value {numericValue} does not fit in {byteAttribute.BitLength} bits.");
            }

            var shiftedMask = mask << byteAttribute.BitIndex;
            data[byteAttribute.ByteIndex] = (byte)((data[byteAttribute.ByteIndex] & ~shiftedMask) | ((numericValue & mask) << byteAttribute.BitIndex));
            return;
        }

        if (layout.BytesAttribute is BytesAttribute bytesAttribute)
        {
            EnsureRangeAvailable(data, bytesAttribute.ByteIndex, bytesAttribute.Length);
            if (value is not byte[] buffer)
            {
                throw new InvalidOperationException($"Member {layout.Member.Name} must be of type byte[] when using {nameof(BytesAttribute)}.");
            }

            if (buffer.Length != bytesAttribute.Length)
            {
                throw new ArgumentException($"Member {layout.Member.Name} must provide exactly {bytesAttribute.Length} bytes.", layout.Member.Name);
            }

            Array.Copy(buffer, 0, data, bytesAttribute.ByteIndex, bytesAttribute.Length);
            return;
        }

        if (layout.ByteListAttribute is ByteListAttribute byteListAttribute)
        {
            if (value is not byte[] buffer)
            {
                throw new InvalidOperationException($"Member {layout.Member.Name} must be of type byte[] when using {nameof(ByteListAttribute)}.");
            }

            EnsureRangeAvailable(data, layout.ByteIndex, layout.ByteLength);
            WriteLengthValue(data, layout.ByteIndex, buffer.Length, byteListAttribute);
            Array.Copy(buffer, 0, data, layout.ValueByteIndex, buffer.Length);
            return;
        }

        if (layout.RefByteListAttribute is RefByteListAttribute)
        {
            if (value is not byte[] buffer)
            {
                throw new InvalidOperationException($"Member {layout.Member.Name} must be of type byte[] when using {nameof(RefByteListAttribute)}.");
            }

            EnsureRangeAvailable(data, layout.ValueByteIndex, layout.ValueByteLength);
            Array.Copy(buffer, 0, data, layout.ValueByteIndex, buffer.Length);
            return;
        }

        if (layout.WordAttribute is WordAttribute wordAttribute)
        {
            EnsureRangeAvailable(data, wordAttribute.ByteIndex, sizeof(ushort));
            var buffer = GetWordBytes(value, layout.MemberType);
            Array.Copy(buffer, 0, data, wordAttribute.ByteIndex, buffer.Length);
            return;
        }

        if (layout.DWordAttribute is DWordAttribute dwordAttribute)
        {
            ValidateDWordLength(dwordAttribute.Length);
            EnsureRangeAvailable(data, dwordAttribute.ByteIndex, dwordAttribute.Length);
            var buffer = GetDWordBytes(value, layout.MemberType, dwordAttribute.Length);
            Array.Copy(buffer, 0, data, dwordAttribute.ByteIndex, buffer.Length);
            return;
        }

        throw new InvalidOperationException($"Member {layout.Member.Name} does not have a supported serializer attribute.");
    }

    private static object ReadWordValue(Type memberType, byte[] data, int byteIndex)
    {
        if (memberType == typeof(short))
        {
            return BigEndianBitConverter.ToInt16(data, byteIndex);
        }

        if (memberType == typeof(ushort))
        {
            return BigEndianBitConverter.ToUInt16(data, byteIndex);
        }

        return ConvertNumericValue(memberType, BigEndianBitConverter.ToUInt16(data, byteIndex));
    }

    private static object ReadDWordValue(Type memberType, byte[] data, int byteIndex, int length)
    {
        var rawValue = ReadDWordRawValue(data, byteIndex, length);

        if (length == sizeof(uint) && memberType == typeof(int))
        {
            return unchecked((int)rawValue);
        }

        if (memberType == typeof(int))
        {
            return checked((int)rawValue);
        }

        if (memberType == typeof(uint))
        {
            return rawValue;
        }

        return ConvertNumericValue(memberType, rawValue);
    }

    private static void SetMemberValue(object instance, MemberInfo member, object value)
    {
        switch (member)
        {
            case FieldInfo fieldInfo:
                fieldInfo.SetValue(instance, value);
                break;

            case PropertyInfo propertyInfo when propertyInfo.SetMethod != null:
                propertyInfo.SetValue(instance, value);
                break;

            case PropertyInfo propertyInfo:
                throw new InvalidOperationException($"Property {propertyInfo.Name} does not have a setter.");

            default:
                throw new InvalidOperationException($"Unsupported member type: {member.MemberType}.");
        }
    }

    private static object? GetMemberValue(object instance, MemberInfo member)
    {
        return member switch
        {
            FieldInfo fieldInfo => fieldInfo.GetValue(instance),
            PropertyInfo propertyInfo when propertyInfo.GetMethod != null => propertyInfo.GetValue(instance),
            PropertyInfo propertyInfo => throw new InvalidOperationException($"Property {propertyInfo.Name} does not have a getter."),
            _ => throw new InvalidOperationException($"Unsupported member type: {member.MemberType}.")
        };
    }

    private static Type GetMemberType(MemberInfo member)
    {
        return member switch
        {
            FieldInfo fieldInfo => fieldInfo.FieldType,
            PropertyInfo propertyInfo => propertyInfo.PropertyType,
            _ => throw new InvalidOperationException($"Unsupported member type: {member.MemberType}.")
        };
    }

    private static object ConvertNumericValue(Type targetType, object value)
    {
        if (targetType == typeof(bool))
        {
            return Convert.ToUInt64(value) != 0;
        }

        if (targetType.IsEnum)
        {
            return Enum.ToObject(targetType, value);
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return Convert.ChangeType(value, underlyingType);
    }

    private static IEnumerable<StructFieldLayout> GetFieldLayouts(Type type, MSBFirstStructAttribute structAttribute, byte[]? data = null, object? instance = null)
    {
        var previousEndByteIndex = 0;
        foreach (var member in GetAnnotatedMembers(type))
        {
            var layout = CreateLayout(member, previousEndByteIndex, data, instance);
            previousEndByteIndex = Math.Max(previousEndByteIndex, layout.ByteIndex + layout.ByteLength);
            yield return layout;
        }
    }

    private static StructFieldLayout CreateLayout(MemberInfo member, int previousEndByteIndex, byte[]? data, object? instance)
    {
        var memberType = GetMemberType(member);
        var declaringType = member.DeclaringType
            ?? throw new InvalidOperationException($"Member {member.Name} does not have a declaring type.");
        var layout = new StructFieldLayout
        {
            Member = member,
            MemberType = memberType,
            BitAttribute = member.GetCustomAttribute<BitAttribute>(),
            ByteAttribute = member.GetCustomAttribute<ByteAttribute>(),
            BytesAttribute = member.GetCustomAttribute<BytesAttribute>(),
            WordAttribute = member.GetCustomAttribute<WordAttribute>(),
            DWordAttribute = member.GetCustomAttribute<DWordAttribute>(),
            RefByteListAttribute = member.GetCustomAttribute<RefByteListAttribute>(),
            ByteListAttribute = member.GetCustomAttribute<ByteListAttribute>(),
            MetadataAttribute = member.GetCustomAttribute<MetadataAttribute>(),
        };

        if (layout.BitAttribute is BitAttribute bitAttribute)
        {
            layout.Encoding = "bit";
            layout.ByteIndex = bitAttribute.ByteIndex;
            layout.ByteLength = 1;
            layout.BitIndex = bitAttribute.BitIndex;
            layout.BitLength = 1;
            return layout;
        }

        if (layout.ByteAttribute is ByteAttribute byteAttribute)
        {
            ValidateBitWindow(byteAttribute.BitIndex, byteAttribute.BitLength);
            layout.Encoding = "byte";
            layout.ByteIndex = byteAttribute.ByteIndex;
            layout.ByteLength = 1;
            layout.BitIndex = byteAttribute.BitIndex;
            layout.BitLength = byteAttribute.BitLength;
            return layout;
        }

        if (layout.BytesAttribute is BytesAttribute bytesAttribute)
        {
            layout.Encoding = "bytes";
            layout.ByteIndex = bytesAttribute.ByteIndex;
            layout.ByteLength = bytesAttribute.Length;
            return layout;
        }

        if (layout.WordAttribute is WordAttribute wordAttribute)
        {
            layout.Encoding = "word";
            layout.ByteIndex = wordAttribute.ByteIndex;
            layout.ByteLength = sizeof(ushort);
            return layout;
        }

        if (layout.DWordAttribute is DWordAttribute dwordAttribute)
        {
            layout.Encoding = "dword";
            layout.ByteIndex = dwordAttribute.ByteIndex;
            layout.ByteLength = dwordAttribute.Length;
            return layout;
        }

        if (layout.RefByteListAttribute is RefByteListAttribute refByteListAttribute)
        {
            layout.Encoding = "refByteList";
            layout.LengthSource = "member";
            layout.LengthFieldMemberName = refByteListAttribute.RefField;
            layout.ByteIndex = refByteListAttribute.ByteIndex >= 0 ? refByteListAttribute.ByteIndex : previousEndByteIndex;

            var referencedLengthLayout = CreateReferencedLengthLayout(declaringType, refByteListAttribute.RefField);
            layout.LengthFieldByteIndex = referencedLengthLayout.ByteIndex;
            layout.LengthFieldByteLength = referencedLengthLayout.ByteLength;
            layout.LengthFieldEncoding = referencedLengthLayout.Encoding;
            layout.ValueByteIndex = layout.ByteIndex;
            layout.ValueByteLength = ResolveReferencedByteListValueLength(layout, data, instance);
            layout.ByteLength = layout.ValueByteLength;
            return layout;
        }

        if (layout.ByteListAttribute is ByteListAttribute byteListAttribute)
        {
            layout.Encoding = "byteList";
            layout.LengthSource = "prefix";
            layout.LengthPrefixByteLength = GetLengthPrefixByteLength(byteListAttribute.LengthType);
            layout.ByteIndex = byteListAttribute.ByteIndex >= 0 ? byteListAttribute.ByteIndex : previousEndByteIndex;
            layout.LengthFieldByteIndex = layout.ByteIndex;
            layout.LengthFieldByteLength = layout.LengthPrefixByteLength;
            layout.LengthFieldEncoding = GetLengthEncoding(byteListAttribute.LengthType);
            layout.IsLengthMSBFirst = byteListAttribute.IsLengthMSBFirst;
            layout.ValueByteLength = ResolveByteListValueLength(layout, data, instance);
            layout.ValueByteIndex = layout.ByteIndex + layout.LengthPrefixByteLength;
            layout.ByteLength = layout.LengthPrefixByteLength + layout.ValueByteLength;
            return layout;
        }

        throw new InvalidOperationException($"Member {member.Name} does not have a supported parser attribute.");
    }

    private static int GetStructByteLength(IEnumerable<StructFieldLayout> layouts, MSBFirstStructAttribute? structAttribute = null)
    {
        return Math.Max(
            layouts.Select(static layout => layout.ByteIndex + layout.ByteLength).DefaultIfEmpty(0).Max(),
            structAttribute?.ExplicitByteLength ?? 0);
    }

    private static int ResolveByteListValueLength(StructFieldLayout layout, byte[]? data, object? instance)
    {
        if (instance != null)
        {
            if (GetMemberValue(instance, layout.Member) is not byte[] bytes)
            {
                throw new InvalidOperationException($"Member {layout.Member.Name} must be of type byte[] when using {nameof(ByteListAttribute)}.");
            }

            return bytes.Length;
        }

        if (data == null)
        {
            throw new InvalidOperationException($"Unable to resolve byte list length for member {layout.Member.Name} without source data.");
        }

        var byteListAttribute = layout.ByteListAttribute
            ?? throw new InvalidOperationException($"Member {layout.Member.Name} is missing {nameof(ByteListAttribute)}.");

        EnsureRangeAvailable(data, layout.ByteIndex, layout.LengthPrefixByteLength);
        var valueByteLength = ReadLengthValue(data, layout.ByteIndex, byteListAttribute);
        EnsureRangeAvailable(data, layout.ByteIndex, layout.LengthPrefixByteLength + valueByteLength);
        return valueByteLength;
    }

    private static int ResolveReferencedByteListValueLength(StructFieldLayout layout, byte[]? data, object? instance)
    {
        if (instance != null)
        {
            if (GetMemberValue(instance, layout.Member) is not byte[] bytes)
            {
                throw new InvalidOperationException($"Member {layout.Member.Name} must be of type byte[] when using {nameof(RefByteListAttribute)}.");
            }

            return bytes.Length;
        }

        if (data == null)
        {
            throw new InvalidOperationException($"Unable to resolve referenced byte list length for member {layout.Member.Name} without source data.");
        }

        var refByteListAttribute = layout.RefByteListAttribute
            ?? throw new InvalidOperationException($"Member {layout.Member.Name} is missing {nameof(RefByteListAttribute)}.");

        var lengthMemberLayout = CreateReferencedLengthLayout(
            layout.Member.DeclaringType ?? throw new InvalidOperationException($"Member {layout.Member.Name} does not have a declaring type."),
            refByteListAttribute.RefField);

        EnsureRangeAvailable(data, lengthMemberLayout.ByteIndex, lengthMemberLayout.ByteLength);
        var valueByteLength = ReadReferencedLengthValue(data, lengthMemberLayout);
        EnsureRangeAvailable(data, layout.ByteIndex, valueByteLength);
        return valueByteLength;
    }

    private static int GetLengthPrefixByteLength(LengthType lengthType)
    {
        return lengthType switch
        {
            LengthType.Byte => 1,
            LengthType.Word => 2,
            LengthType.Dword => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(lengthType), lengthType, "Unsupported list length type."),
        };
    }

    private static string GetLengthEncoding(LengthType lengthType)
    {
        return lengthType switch
        {
            LengthType.Byte => "byte",
            LengthType.Word => "word",
            LengthType.Dword => "dword",
            _ => throw new ArgumentOutOfRangeException(nameof(lengthType), lengthType, "Unsupported list length type."),
        };
    }

    private static StructFieldLayout CreateReferencedLengthLayout(Type declaringType, string memberName)
    {
        var member = declaringType.GetMember(memberName, MemberBindingFlags).FirstOrDefault()
            ?? throw new InvalidOperationException($"Unable to find referenced length member {memberName} on type {declaringType.FullName}.");

        var memberType = GetMemberType(member);
        var layout = new StructFieldLayout
        {
            Member = member,
            MemberType = memberType,
            ByteAttribute = member.GetCustomAttribute<ByteAttribute>(),
            WordAttribute = member.GetCustomAttribute<WordAttribute>(),
            DWordAttribute = member.GetCustomAttribute<DWordAttribute>(),
        };

        if (layout.ByteAttribute is ByteAttribute byteAttribute)
        {
            if (byteAttribute.BitIndex != 0 || byteAttribute.BitLength != 8)
            {
                throw new InvalidOperationException($"Referenced length member {member.Name} must occupy a full byte.");
            }

            layout.Encoding = "byte";
            layout.ByteIndex = byteAttribute.ByteIndex;
            layout.ByteLength = 1;
            return layout;
        }

        if (layout.WordAttribute is WordAttribute wordAttribute)
        {
            layout.Encoding = "word";
            layout.ByteIndex = wordAttribute.ByteIndex;
            layout.ByteLength = sizeof(ushort);
            return layout;
        }

        if (layout.DWordAttribute is DWordAttribute dwordAttribute)
        {
            layout.Encoding = "dword";
            layout.ByteIndex = dwordAttribute.ByteIndex;
            layout.ByteLength = dwordAttribute.Length;
            return layout;
        }

        throw new InvalidOperationException($"Referenced length member {member.Name} must use {nameof(ByteAttribute)}, {nameof(WordAttribute)} or {nameof(DWordAttribute)}.");
    }

    private static int ReadReferencedLengthValue(byte[] data, StructFieldLayout layout)
    {
        return layout.Encoding switch
        {
            "byte" => data[layout.ByteIndex],
            "word" => BigEndianBitConverter.ToUInt16(data, layout.ByteIndex),
            "dword" => checked((int)ReadDWordRawValue(data, layout.ByteIndex, layout.ByteLength)),
            _ => throw new InvalidOperationException($"Unsupported referenced length encoding {layout.Encoding}.")
        };
    }

    private static void SynchronizeReferencedByteListLengths(object instance, StructFieldLayout[] layouts)
    {
        var members = layouts.ToDictionary(static layout => layout.Member.Name, static layout => layout.Member, StringComparer.Ordinal);

        foreach (var layout in layouts)
        {
            if (layout.RefByteListAttribute is not RefByteListAttribute refByteListAttribute)
            {
                continue;
            }

            if (GetMemberValue(instance, layout.Member) is not byte[] bytes)
            {
                throw new InvalidOperationException($"Member {layout.Member.Name} must be of type byte[] when using {nameof(RefByteListAttribute)}.");
            }

            if (!members.TryGetValue(refByteListAttribute.RefField, out var referencedMember))
            {
                referencedMember = layout.Member.DeclaringType?.GetMember(refByteListAttribute.RefField, MemberBindingFlags).FirstOrDefault()
                    ?? throw new InvalidOperationException($"Unable to find referenced length member {refByteListAttribute.RefField} on type {layout.Member.DeclaringType?.FullName}.");
            }

            SetMemberValue(instance, referencedMember, ConvertNumericValue(GetMemberType(referencedMember), bytes.Length));
        }
    }

    private static int ReadLengthValue(byte[] data, int byteIndex, ByteListAttribute attribute)
    {
        return attribute.LengthType switch
        {
            LengthType.Byte => data[byteIndex],
            LengthType.Word => attribute.IsLengthMSBFirst
                ? BigEndianBitConverter.ToUInt16(data, byteIndex)
                : data[byteIndex] | (data[byteIndex + 1] << 8),
            LengthType.Dword => checked((int)(attribute.IsLengthMSBFirst
                ? BigEndianBitConverter.ToUInt32(data, byteIndex)
                : (uint)(data[byteIndex] | (data[byteIndex + 1] << 8) | (data[byteIndex + 2] << 16) | (data[byteIndex + 3] << 24)))),
            _ => throw new ArgumentOutOfRangeException(nameof(attribute.LengthType), attribute.LengthType, "Unsupported list length type."),
        };
    }

    private static void WriteLengthValue(byte[] data, int byteIndex, int value, ByteListAttribute attribute)
    {
        switch (attribute.LengthType)
        {
            case LengthType.Byte:
                if (value > byte.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Byte list length exceeds one-byte capacity.");
                }
                data[byteIndex] = (byte)value;
                return;

            case LengthType.Word:
            {
                if (value > ushort.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Byte list length exceeds two-byte capacity.");
                }

                var buffer = attribute.IsLengthMSBFirst
                    ? BigEndianBitConverter.GetBytes((ushort)value)
                    : new[] { (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF) };
                Array.Copy(buffer, 0, data, byteIndex, buffer.Length);
                return;
            }

            case LengthType.Dword:
            {
                var buffer = attribute.IsLengthMSBFirst
                    ? BigEndianBitConverter.GetBytes((uint)value)
                    : new[]
                    {
                        (byte)(value & 0xFF),
                        (byte)((value >> 8) & 0xFF),
                        (byte)((value >> 16) & 0xFF),
                        (byte)((value >> 24) & 0xFF),
                    };
                Array.Copy(buffer, 0, data, byteIndex, buffer.Length);
                return;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(attribute.LengthType), attribute.LengthType, "Unsupported list length type.");
        }
    }

    private static byte[] GetWordBytes(object value, Type memberType)
    {
        if (memberType == typeof(short))
        {
            return BigEndianBitConverter.GetBytes(Convert.ToInt16(value, CultureInfo.InvariantCulture));
        }

        return BigEndianBitConverter.GetBytes(Convert.ToUInt16(value, CultureInfo.InvariantCulture));
    }

    private static byte[] GetDWordBytes(object value, Type memberType, int length)
    {
        ValidateDWordLength(length);

        uint numericValue;
        if (memberType == typeof(int))
        {
            numericValue = unchecked((uint)Convert.ToInt32(value, CultureInfo.InvariantCulture));
        }
        else
        {
            numericValue = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
        }

        ValidateDWordValueFits(numericValue, length, memberType, value);

        var buffer = new byte[length];
        for (var index = 0; index < length; index++)
        {
            var shift = (length - index - 1) * 8;
            buffer[index] = (byte)((numericValue >> shift) & 0xFF);
        }

        return buffer;
    }

    private static bool ConvertToBoolean(object value)
    {
        if (value is bool boolValue)
        {
            return boolValue;
        }

        return Convert.ToUInt64(value, CultureInfo.InvariantCulture) != 0;
    }

    private static void EnsureByteAvailable(byte[] data, int byteIndex)
    {
        if (byteIndex < 0 || byteIndex >= data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(byteIndex), $"Byte index {byteIndex} is outside the data buffer.");
        }
    }

    private static void EnsureRangeAvailable(byte[] data, int byteIndex, int length)
    {
        if (byteIndex < 0 || length < 0 || byteIndex + length > data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(byteIndex), $"Range [{byteIndex}, {byteIndex + length}) is outside the data buffer.");
        }
    }

    private static void ValidateBitWindow(int bitIndex, int bitLength)
    {
        if (bitIndex < 0 || bitIndex > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(bitIndex), "Bit index must be between 0 and 7.");
        }

        if (bitLength <= 0 || bitLength > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(bitLength), "Bit length must be between 1 and 8.");
        }

        if (bitIndex + bitLength > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(bitLength), "Bit window cannot extend beyond a single byte.");
        }
    }

    private static uint ReadDWordRawValue(byte[] data, int byteIndex, int length)
    {
        ValidateDWordLength(length);

        uint value = 0;
        for (var index = 0; index < length; index++)
        {
            value = (value << 8) | data[byteIndex + index];
        }

        return value;
    }

    private static void ValidateDWordLength(int length)
    {
        if (length <= 0 || length > sizeof(uint))
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, $"{nameof(DWordAttribute)} length must be between 1 and {sizeof(uint)} bytes.");
        }
    }

    private static void ValidateDWordValueFits(uint value, int length, Type memberType, object originalValue)
    {
        if (length == sizeof(uint))
        {
            return;
        }

        var bitCount = length * 8;
        var maxValue = (1u << bitCount) - 1;
        if (value > maxValue)
        {
            throw new ArgumentOutOfRangeException(memberType.Name, originalValue, $"Value {value} does not fit in {length} bytes.");
        }
    }
}

internal sealed class StructFieldLayout
{
    public required MemberInfo Member { get; init; }
    public required Type MemberType { get; init; }
    public BitAttribute? BitAttribute { get; init; }
    public ByteAttribute? ByteAttribute { get; init; }
    public BytesAttribute? BytesAttribute { get; init; }
    public WordAttribute? WordAttribute { get; init; }
    public DWordAttribute? DWordAttribute { get; init; }
    public RefByteListAttribute? RefByteListAttribute { get; init; }
    public ByteListAttribute? ByteListAttribute { get; init; }
    public MetadataAttribute? MetadataAttribute { get; init; }
    public string Encoding { get; set; } = string.Empty;
    public string? LengthSource { get; set; }
    public int ByteIndex { get; set; }
    public int ByteLength { get; set; }
    public int LengthPrefixByteLength { get; set; }
    public int LengthFieldByteIndex { get; set; }
    public int LengthFieldByteLength { get; set; }
    public string? LengthFieldEncoding { get; set; }
    public string? LengthFieldMemberName { get; set; }
    public bool? IsLengthMSBFirst { get; set; }
    public int ValueByteIndex { get; set; }
    public int ValueByteLength { get; set; }
    public int? BitIndex { get; set; }
    public int? BitLength { get; set; }
}