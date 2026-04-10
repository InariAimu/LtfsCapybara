using System;
using System.Text;
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

    public string? this[string key]
    {
        get
        {
            if (Xattrs == null) return null;
            foreach (var xa in Xattrs)
            {
                if (xa?.Key?.Value != key) continue;
                var value = xa.Value;
                var val = value?.Value;
                if (value == null || val == null) return null;
                switch (value.Type)
                {
                    case XAttrValueType.base64:
                        try
                        {
                            var bytes = Convert.FromBase64String(val);
                            return Encoding.UTF8.GetString(bytes);
                        }
                        catch
                        {
                            return null;
                        }
                    case XAttrValueType.text:
                    default:
                        return val;
                }
            }
            return null;
        }
    }
}
