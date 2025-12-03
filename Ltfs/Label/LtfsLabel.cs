using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml.Schema;
using System.Xml;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using Ltfs.Index;
using Ltfs.Utils;

namespace Ltfs.Label;

/// <summary>
/// Represents the root element 'ltfslabel' in the XML schema.
/// </summary>
[XmlRoot(ElementName = "ltfslabel", Namespace = "", IsNullable = false)]
public class LtfsLabel : ICloneable
{
    [XmlElement("creator", Form = XmlSchemaForm.Unqualified)]
    public required string Creator { get; set; }

    [XmlElement("formattime", Form = XmlSchemaForm.Unqualified)]
    public required XDateTime Formattime { get; set; }

    [XmlElement("volumeuuid", Form = XmlSchemaForm.Unqualified)]
    public required Guid Volumeuuid { get; set; }

    [XmlElement("location", Form = XmlSchemaForm.Unqualified)]
    public required Location Location { get; set; }

    [XmlElement("partitions", Form = XmlSchemaForm.Unqualified)]
    public required Partitions Partitions { get; set; }

    [XmlElement("blocksize", Form = XmlSchemaForm.Unqualified)]
    public required int Blocksize { get; set; }

    [XmlElement("compression", Form = XmlSchemaForm.Unqualified)]
    public required bool Compression { get; set; } = true;

    /// <summary>
    /// Gets or sets the 'version' attribute.
    /// Must follow the pattern [0-9]+\.[0-9]+\.[0-9+] and can be one of the enumerated values like "2.4.0".
    /// </summary>
    [XmlAttribute("version", Form = XmlSchemaForm.Unqualified)]
    public required string Version { get; set; }


    public static LtfsLabel Default()
    {
        return new LtfsLabel()
        {
            Version = "2.4.0",
            Creator = string.Empty,
            Formattime = DateTime.UtcNow,
            Volumeuuid = Guid.NewGuid(),
            Location = new Location(),
            Partitions = new Partitions(),
            Blocksize = 524288,
            Compression = true,
        };
    }
    public object Clone()
    {
        return LtfsLabel.FromXml(LtfsLabel.ToXml(this));
    }

    public static LtfsLabel? FromXml(string xml)
    {
        XmlSerializer serializer = new(typeof(LtfsLabel));

        using StringReader reader = new(xml);
        LtfsLabel? label = (LtfsLabel?)serializer.Deserialize(reader);

        return label;
    }

    public static LtfsLabel? FromByteArray(byte[] data)
    {
        string text = Encoding.UTF8.GetString(data).TrimEnd();
        return FromXml(text);
    }

    public static string ToXml(LtfsLabel ltfsLabel)
    {
        XmlSerializer serializer = new(typeof(LtfsLabel));
        XmlSerializerNamespaces namespaces = new();
        namespaces.Add(string.Empty, string.Empty); // Remove xmlns:xsi and xmlns:xsd

        XmlWriterSettings settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = true, // We'll add declaration manually with uppercase UTF-8
            Indent = false,
            NewLineHandling = NewLineHandling.None,
        };

        using MemoryStream ms = new();
        using (XmlWriter writer = XmlWriter.Create(ms, settings))
        {
            serializer.Serialize(writer, ltfsLabel, namespaces);
            writer.Flush();
        }

        string body = Encoding.UTF8.GetString(ms.ToArray());
        string declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
        return declaration + body;
    }

    public static byte[] ToByteArray(LtfsLabel ltfsLabel)
    {
        return Encoding.UTF8.GetBytes(ToXml(ltfsLabel));
    }

}
