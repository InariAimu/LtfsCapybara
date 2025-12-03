using System.Xml.Serialization;

namespace Ltfs.Index;

[Serializable()]
[System.Diagnostics.DebuggerStepThrough()]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true)]
[XmlRoot(Namespace = "", IsNullable = false)]
public partial class Symlink
{
    [XmlText()]
    public required string Value { get; set; }
}
