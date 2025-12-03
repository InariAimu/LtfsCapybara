using System.Xml.Serialization;
using System.Xml.Schema;
using System.Xml;
using System.Text;
using Ltfs.Utils;

namespace Ltfs.Index;


[Serializable()]
[System.Diagnostics.DebuggerStepThrough()]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true)]
[XmlRoot("ltfsindex", Namespace = "", IsNullable = false)]
public partial class LtfsIndex : ICloneable
{
    [XmlElement("creator", Form = XmlSchemaForm.Unqualified)]
    public required string Creator { get; set; }


    [XmlElement("comment", Form = XmlSchemaForm.Unqualified, IsNullable = true)]
    public string? Comment { get; set; }
    public bool ShouldSerializeComment() => Comment is not null;


    [XmlElement("volumeuuid", Form = XmlSchemaForm.Unqualified)]
    public required Guid VolumeUUID { get; set; }


    [XmlElement("generationnumber", Form = XmlSchemaForm.Unqualified)]
    public required uint GenerationNumber { get; set; } = 1;


    [XmlElement("updatetime", Form = XmlSchemaForm.Unqualified)]
    public required XDateTime UpdateTime { get; set; }


    [XmlElement("location", Form = XmlSchemaForm.Unqualified)]
    public required TapePosition Location { get; set; }


    [XmlElement("previousgenerationlocation", Form = XmlSchemaForm.Unqualified, IsNullable = true)]
    public TapePosition? PreviousGenerationLocation { get; set; }
    public bool ShouldSerializePreviousGenerationLocation() => PreviousGenerationLocation is not null;


    /// <summary>
    /// This element shall contain a value conforming to the boolean format definition
    /// provided in Section 7.1 Boolean format. When the allowpolicyupdate value is set, the writer may
    /// change the content of the dataplacementpolicy element. When the allowpolicyupdate value is 
    /// unset, the writer shall not change the content of the dataplacementpolicy element. Additional rules 
    /// for the allowpolicyupdate element are provided in Section 9.2.11 Data Placement Policy.
    /// </summary>
    [XmlElement("allowpolicyupdate", Form = XmlSchemaForm.Unqualified)]
    public required bool AllowPolicyUpdate { get; set; }


    [XmlElement("dataplacementpolicy", Form = XmlSchemaForm.Unqualified, IsNullable = true)]
    public DataPlacementPolicy? DataPlacementPolicy { get; set; }
    public bool ShouldSerializeDataPlacementPolicy() => DataPlacementPolicy is not null;


    [XmlElement("volumelockstate", Form = XmlSchemaForm.Unqualified, IsNullable = true)]
    public LockType? VolumeLockState { get; set; }
    public bool ShouldSerializeVolumeLockState() => VolumeLockState is not null;


    [XmlElement("highestfileuid", Form = XmlSchemaForm.Unqualified)]
    public required UInt64 HighestFileUID { get; set; }


    [XmlElement("directory", Form = XmlSchemaForm.Unqualified)]
    public required LtfsDirectory Directory { get; set; }


    [XmlIgnore]
    public LtfsDirectory Root { get => Directory; }


    [XmlAttribute("version", Form = XmlSchemaForm.Unqualified)]
    public string Version { get; set; } = "2.4.0";


    public static LtfsIndex Default()
    {
        return new()
        {
            AllowPolicyUpdate = true,
            Creator = "LTFSmeow",
            VolumeUUID = Guid.NewGuid(),
            UpdateTime = DateTime.UtcNow,
            HighestFileUID = 1,
            GenerationNumber = 1,
            Location = new TapePosition { Partition = "a", StartBlock = 3 },
            Directory = new LtfsDirectory
            {
                Name = new NameType { Value = "/" },
                FileUID = 1,
                CreationTime = DateTime.UtcNow,
                ChangeTime = DateTime.UtcNow,
                ModifyTime = DateTime.UtcNow,
                AccessTime = DateTime.UtcNow,
                BackupTime = DateTime.UtcNow,
                ReadOnly = false,
                Contents = Array.Empty<object>(),
            },
        };
    }

    public object Clone()
    {
        return LtfsIndex.FromXml(LtfsIndex.ToXml(this));
    }

    public static LtfsIndex? FromXml(string xml)
    {
        XmlSerializer serializer = new(typeof(LtfsIndex));

        using StringReader reader = new(xml);
        LtfsIndex? label = (LtfsIndex?)serializer.Deserialize(reader);

        return label;
    }

    public static LtfsIndex? FromByteArray(byte[] data)
    {
        string text = Encoding.UTF8.GetString(data).TrimEnd();
        return FromXml(text);
    }

    public static string ToXml(LtfsIndex index)
    {
        XmlSerializer serializer = new(typeof(LtfsIndex));
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
            serializer.Serialize(writer, index, namespaces);
            writer.Flush();
        }

        string body = Encoding.UTF8.GetString(ms.ToArray());
        string declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
        return declaration + body;
    }

    public static byte[] ToByteArray(LtfsIndex index)
    {
        string xml = ToXml(index);
        return Encoding.UTF8.GetBytes(xml);
    }
    
}
