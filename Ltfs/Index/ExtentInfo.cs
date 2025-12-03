using System.Xml.Serialization;
using System.Xml.Schema;

namespace Ltfs.Index;


[Serializable()]
[System.Diagnostics.DebuggerStepThrough()]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true)]
[XmlRoot(Namespace = "", IsNullable = false)]
public partial class ExtentInfo
{
    [XmlElement("extent", Form = XmlSchemaForm.Unqualified, Order = 0)]
    public required Extent[] Extent { get; set; }
}
