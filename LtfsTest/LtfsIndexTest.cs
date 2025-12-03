using System;
using System.Reflection.Emit;
using System.Xml.Schema;
using System.Xml;
using System.Xml.Serialization;
using Xunit.Abstractions;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Ltfs.Label;
using Ltfs.Index;
using Ltfs.Utils;
using System.Data.SqlTypes;

namespace LtfsTest;

public class LtfsIndexTest
{
    private readonly ITestOutputHelper _output;

    public LtfsIndexTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void XDateTime()
    {
        var dt = DateTime.UtcNow;
        var xdt = new XDateTime(dt);
        var xs = xdt.ToString();
        _output.WriteLine(xs);
        Assert.True(Int32.TryParse(xs[^7..^1], out _));
    }

    [Fact]
    public void Deserialize()
    {
        string x = File.ReadAllText(@"..\..\..\index.xml");
        var schemas = new XmlSchemaSet();
        schemas.Add("", @"..\..\..\LtfsIndex.xsd");

        bool hasValidationErrors = false;
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemas
        };
        settings.ValidationEventHandler += (sender, e) =>
        {
            _output.WriteLine(e.Message);
            hasValidationErrors = true;
        };

        using (var reader = XmlReader.Create(new StringReader(x), settings))
        {
            while (reader.Read()) { }
        }

        Assert.False(hasValidationErrors);
    }

    [Fact]
    public void Xsd()
    {
        string x = File.ReadAllText(@"..\..\..\index.xml");
        var index = LtfsIndex.FromXml(x);
        Assert.NotNull(index);
        Assert.True(index.Creator == "LTFSCopyGUI 3.5.4 - Windows - TapeUtils");
    }

    [Fact]
    public void SerializeAndXsd()
    {
        var index = LtfsIndex.Default();
        index.VolumeLockState = LockType.unlocked;
        index.Root.Contents = [
            new LtfsFile() {
                FileUID = 2,
                Name = new NameType { Value = "file1.txt" },
                CreationTime = DateTime.UtcNow,
                ChangeTime = DateTime.UtcNow,
                ModifyTime = DateTime.UtcNow,
                AccessTime = DateTime.UtcNow,
                BackupTime = DateTime.UtcNow,
                ReadOnly = false,
                Length = 12345,
                ExtentInfo = new ExtentInfo() {
                    Extent = [
                        new Extent() {
                            Partition = "b",
                            StartBlock = 999,
                            ByteCount = 12345,
                            ByteOffset = 0,
                            FileOffset = 0
                        }
                    ]
                },
                ExtendedAttributes = new ExtendedAttributes() {
                    Xattrs = [
                        new XAttr("ltfs.hash.crc64sum", "C96C5795D7870F42"),
                    ]
                },
            }
        ];

        string xmlString = LtfsIndex.ToXml(index);

        if (!xmlString.Contains("encoding=\"UTF-8\""))
            Assert.Fail("XML declaration should specify UTF-8 encoding");

        if (xmlString.Contains("xmlns:xsi=") || xmlString.Contains("xmlns:xsd="))
            Assert.Fail("XML should not contain unnecessary namespace declarations");

        var schemas = new XmlSchemaSet();
        schemas.Add("", @"..\..\..\LtfsIndex.xsd");

        bool hasValidationErrors = false;
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemas
        };
        settings.ValidationEventHandler += (sender, e) =>
        {
            _output.WriteLine(e.Message);
            hasValidationErrors = true;
        };

        using (var reader = XmlReader.Create(new StringReader(xmlString), settings))
        {
            while (reader.Read()) { }
        }

        Assert.False(hasValidationErrors);


        LtfsIndex? deserialized = LtfsIndex.FromXml(xmlString);

        Assert.NotNull(deserialized);
    }
}
