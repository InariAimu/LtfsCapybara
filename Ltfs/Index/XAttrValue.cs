using System.Xml.Serialization;

namespace Ltfs.Index;

[Serializable()]
[System.Diagnostics.DebuggerStepThrough()]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public partial class XAttrValue
{
    [XmlAttribute("type")]
    [System.ComponentModel.DefaultValue(XAttrValueType.text)]
    public XAttrValueType Type { get; set; } = XAttrValueType.text;

    [XmlText()]
    public string Value { get; set; }
}
