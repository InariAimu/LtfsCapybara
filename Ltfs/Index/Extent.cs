using System.Xml.Serialization;
using System.Xml.Schema;

namespace Ltfs.Index;

[Serializable()]
[System.Diagnostics.DebuggerStepThrough()]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public partial class Extent
{
    [XmlElement("partition", Form = XmlSchemaForm.Unqualified)]
    public required string Partition { get; set; }


    [XmlElement("startblock", Form = XmlSchemaForm.Unqualified)]
    public required UInt64 StartBlock { get; set; }


    [XmlElement("byteoffset", Form = XmlSchemaForm.Unqualified)]
    public required UInt64 ByteOffset { get; set; }


    [XmlElement("bytecount", Form = XmlSchemaForm.Unqualified)]
    public required UInt64 ByteCount { get; set; }


    [XmlElement("fileoffset", Form = XmlSchemaForm.Unqualified)]
    public required UInt64 FileOffset { get; set; }
}
