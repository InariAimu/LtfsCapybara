
using System.Reflection;
using System.Text.Json;

namespace TapeDrive.Utils;

public static partial class StructParser
{
    private const BindingFlags MemberBindingFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static T Parse<T>(byte[] data) where T : new()
    {
        ArgumentNullException.ThrowIfNull(data);
        return (T)Parse(typeof(T), data);
    }

    public static object Parse(Type type, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(data);

        var structAttribute = EnsureStructType(type);

        var instance = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Unable to create instance of {type.FullName}.");

        foreach (var layout in GetFieldLayouts(type, structAttribute, data: data))
        {
            var value = ReadAnnotatedValue(layout, data);
            SetMemberValue(instance, layout.Member, value);
        }

        return instance;
    }

    public static byte[] ToBytes<T>(T instance) where T : notnull
    {
        return ToBytes((object)instance);
    }

    public static byte[] ToBytes(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var type = instance.GetType();
        var structAttribute = EnsureStructType(type);

        var layouts = GetFieldLayouts(type, structAttribute, instance: instance).ToArray();
        var bytes = new byte[GetStructByteLength(layouts, structAttribute)];

        foreach (var layout in layouts)
        {
            WriteAnnotatedValue(layout, bytes, GetMemberValue(instance, layout.Member));
        }

        return bytes;
    }

    public static StructMetadataDocument ToMetadataDocument<T>(T instance) where T : notnull
    {
        return ToMetadataDocument((object)instance);
    }

    public static StructMetadataDocument ToMetadataDocument(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var type = instance.GetType();
        var structAttribute = EnsureStructType(type);

        var layouts = GetFieldLayouts(type, structAttribute, instance: instance).ToArray();
        var bytes = ToBytes(instance);
        var fields = layouts
            .Select(layout => CreateMetadataField(instance, layout, bytes))
            .OrderBy(field => field.Location.ByteIndex)
            .ThenBy(field => field.Location.BitIndex ?? 0)
            .ThenBy(field => field.MemberName)
            .ToArray();

        return new StructMetadataDocument
        {
            TypeName = type.Name,
            Description = structAttribute.Description,
            ByteLength = bytes.Length,
            RawBytes = bytes.Select(static value => (int)value).ToArray(),
            RawHex = FormatHex(bytes),
            Fields = fields,
        };
    }

    public static string ToMetadataJson<T>(T instance, JsonSerializerOptions? options = null) where T : notnull
    {
        return ToMetadataJson((object)instance, options);
    }

    public static string ToMetadataJson(object instance, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return JsonSerializer.Serialize(ToMetadataDocument(instance), options ?? DefaultJsonOptions);
    }

    private static MSBFirstStructAttribute EnsureStructType(Type type)
    {
        var attribute = type.GetCustomAttribute<MSBFirstStructAttribute>();
        if (attribute == null)
        {
            throw new ArgumentException($"Type {type.FullName} is not marked with {nameof(MSBFirstStructAttribute)}.", nameof(type));
        }

        return attribute;
    }
}