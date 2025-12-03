using System.Xml.Serialization;

namespace Ltfs.Index;

[Serializable()]
[XmlType(AnonymousType = true)]
public enum XAttrValueType
{
    base64,
    text,
}
