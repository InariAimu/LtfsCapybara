using System;
using System.IO;
using System.Xml.Serialization;
using Xunit;
using Ltfs.Utils;

namespace LtfsTest;

public class XDateTimeTest
{
    [Fact]
    public void ToString_ProducesNineFractionalDigitsAndZ()
    {
        var dt = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc)
            .AddTicks(1234560); // 123456 microseconds -> 1234560 ticks

        var xdt = new XDateTime(dt);

        var s = xdt.ToString();

        Assert.Equal("2020-01-02T03:04:05.123456000Z", s);
    }

    [Fact]
    public void XmlSerialization_RoundTripsValue()
    {
        var dt = new DateTime(2021, 6, 7, 8, 9, 10, DateTimeKind.Utc)
            .AddTicks(6543210); // 654321 microseconds

        var original = new XDateTime(dt);

        var ser = new XmlSerializer(typeof(XDateTime));
        string xml;
        using (var sw = new StringWriter())
        {
            ser.Serialize(sw, original);
            xml = sw.ToString();
        }

        XDateTime deserialized;
        using (var sr = new StringReader(xml))
        {
            deserialized = (XDateTime)ser.Deserialize(sr)!;
        }

        Assert.Equal((DateTime)original, (DateTime)deserialized);
    }
}
