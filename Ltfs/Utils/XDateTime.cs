using System;
using System.Globalization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Ltfs.Utils;

public struct XDateTime : IXmlSerializable
{
    private DateTime _value;

    public XDateTime(DateTime value)
    {
        _value = value;
    }

    // compatitable with DateTime
    public static implicit operator DateTime(XDateTime xdt) => xdt._value;
    public static implicit operator XDateTime(DateTime dt) => new XDateTime(dt);

    public override string ToString()
    {
        // 9-digit fractional seconds + 'Z' suffix
        return _value.ToString("yyyy-MM-ddTHH:mm:ss.ffffff", CultureInfo.InvariantCulture) + "000Z";
    }

    #region IXmlSerializable
    public XmlSchema GetSchema() => null;

    public void ReadXml(XmlReader reader)
    {
        var text = reader.ReadElementContentAsString();
        if (text.EndsWith("Z"))
            text = text[..^4];

        _value = DateTime.ParseExact(
            text,
            "yyyy-MM-ddTHH:mm:ss.ffffff",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None
        );
    }

    public void WriteXml(XmlWriter writer)
    {
        writer.WriteString(ToString());
    }
    #endregion
}
