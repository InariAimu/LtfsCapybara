using System.Xml.Serialization;
using System.Xml.Schema;

namespace Ltfs.Index;

[Serializable()]
[System.Diagnostics.DebuggerStepThrough()]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public partial class IndexPartitionCriteria
{
    [XmlElement("name", Form = XmlSchemaForm.Unqualified)]
    public NameType[] Names { get; set; }


    [XmlElement("size", Form = XmlSchemaForm.Unqualified)]
    public UInt64 Size { get; set; }
}
