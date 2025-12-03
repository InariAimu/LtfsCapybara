using System.Xml.Serialization;
using System.Xml.Schema;
using System.Net.Cache;
using System.Xml.Linq;
using Ltfs.Utils;

namespace Ltfs.Index;

[Serializable()]
[System.Diagnostics.DebuggerStepThrough()]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(TypeName = "file", AnonymousType = true)]
[XmlRoot(Namespace = "", IsNullable = false)]
public partial class LtfsFile
{
    [XmlElement("fileuid", Form = XmlSchemaForm.Unqualified)]
    public required UInt64 FileUID { get; set; }


    [XmlElement("name", Form = XmlSchemaForm.Unqualified)]
    public required NameType Name { get; set; }


    [XmlElement("length", Form = XmlSchemaForm.Unqualified)]
    public required UInt64 Length { get; set; }


    [XmlElement("creationtime", Form = XmlSchemaForm.Unqualified)]
    public required XDateTime CreationTime { get; set; }


    [XmlElement("changetime", Form = XmlSchemaForm.Unqualified)]
    public required XDateTime ChangeTime { get; set; }


    [XmlElement("modifytime", Form = XmlSchemaForm.Unqualified)]
    public required XDateTime ModifyTime { get; set; }


    [XmlElement("accesstime", Form = XmlSchemaForm.Unqualified)]
    public required XDateTime AccessTime { get; set; }


    [XmlElement("backuptime", Form = XmlSchemaForm.Unqualified)]
    public required XDateTime BackupTime { get; set; }


    [XmlElement("readonly", Form = XmlSchemaForm.Unqualified)]
    public required bool ReadOnly { get; set; } = false;


    [XmlElement("extendedattributes", Form = XmlSchemaForm.Unqualified, IsNullable = true)]
    public ExtendedAttributes? ExtendedAttributes { get; set; }

    public bool ShouldSerializeExtendedAttributes() => ExtendedAttributes is not null && ExtendedAttributes.Xattrs.Length > 0;


    [XmlElement("openforwrite", Form = XmlSchemaForm.Unqualified, IsNullable = true)]
    public bool? OpenForWrite { get; set; } = false;


    [XmlElement("extentinfo", Form = XmlSchemaForm.Unqualified, IsNullable = true)]
    public ExtentInfo? ExtentInfo { get; set; }

    public bool ShouldSerializeExtentInfo() => ExtentInfo is not null && ExtentInfo.Extent.Length > 0;


    [XmlElement("symlink", Form = XmlSchemaForm.Unqualified, IsNullable = true)]
    public Symlink? Symlink { get; set; }
    public bool ShouldSerializeSymlink() => Symlink is not null;


    public override string ToString() => Name;


    public static LtfsFile Default()
    {
        return new LtfsFile()
        {
            FileUID = 0,
            Name = "",
            Length = 0,
            CreationTime = DateTime.UtcNow,
            ChangeTime = DateTime.UtcNow,
            ModifyTime = DateTime.UtcNow,
            AccessTime = DateTime.UtcNow,
            BackupTime = DateTime.UtcNow,
            ReadOnly = false,
        };
    }
}
