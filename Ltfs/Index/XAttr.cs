using System.Xml.Serialization;
using System.Xml.Schema;

namespace Ltfs.Index;

[Serializable()]
[System.Diagnostics.DebuggerStepThrough()]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public partial class XAttr
{
    public XAttr()
    {
        Key = new NameType { PercentEncoded = false, Value = "" };
        Value = new XAttrValue() { Type = XAttrValueType.text, Value = "" };
    }

    public XAttr(string key, string value)
    {
        Key = new NameType { PercentEncoded = false, Value = key };
        Value = new XAttrValue() { Type = XAttrValueType.text, Value = value };
    }

    [XmlElement("key", Form = XmlSchemaForm.Unqualified)]
    public NameType Key { get; set; }


    [XmlElement("value", Form = XmlSchemaForm.Unqualified)]
    public XAttrValue Value { get; set; }
}
