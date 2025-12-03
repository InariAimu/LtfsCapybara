using System.Xml.Serialization;
using System.Xml.Schema;

namespace Ltfs.Index;

[Serializable()]
[System.Diagnostics.DebuggerStepThrough()]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true)]
[XmlRoot(Namespace = "", IsNullable = false)]
public partial class ExtendedAttributes
{
    [XmlElement("xattr", Form = XmlSchemaForm.Unqualified)]
    public required XAttr[] Xattrs { get; set; }
}
