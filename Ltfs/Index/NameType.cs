using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Ltfs.Index;

[Serializable()]
[System.Diagnostics.DebuggerStepThrough()]
[System.ComponentModel.DesignerCategory("code")]
[XmlRoot(Namespace = "", IsNullable = false)]
public partial class NameType
{
    public NameType()
    {
        Value = "";
    }

    public NameType(string value)
    {
        Value = value;
    }

    [XmlAttribute("percentencoded")]
    public bool PercentEncoded { get; set; } = false;

    public bool ShouldSerializePercentEncoded()
    {
        return PercentEncoded;
    }

    [XmlText()]
    public required string Value { get; set; }


    public static implicit operator string(NameType nt) => nt.GetName();
    public static implicit operator NameType(string name)
    {
        var nt = new NameType() { Value = "" };
        nt.SetName(name);
        return nt;
    }

    public override string ToString() => GetName();


    public string GetName()
    {
        if (PercentEncoded)
        {
            return Uri.UnescapeDataString(Value);
        }
        else
        {
            return Value;
        }
    }

    public void SetName(string name)
    {
        // percent-encode '%', ':'
        if (name.Contains('%') || name.Contains(':'))
        {
            Value = name.Replace("%", "%25").Replace(":", "%3A");
            PercentEncoded = true;
        }
        else
        {
            Value = name;
            PercentEncoded = false;
        }
    }
}
