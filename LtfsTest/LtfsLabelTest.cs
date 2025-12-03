using System;
using System.Reflection.Emit;
using System.Xml.Schema;
using System.Xml;
using System.Xml.Serialization;
using Xunit.Abstractions;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Ltfs.Label;

namespace LtfsTest;

public class LtfsLabelTest
{
    private readonly ITestOutputHelper _output;

    public LtfsLabelTest(ITestOutputHelper output)
    {
        _output = output;
    }


    [Fact]
    public void SerializeAndXsd()
    {
        DateTime formatTime = DateTime.UtcNow;
        Guid volumeuuid = Guid.NewGuid();

        LtfsLabel LtfsLabel = new LtfsLabel()
        {
            Creator = "LTFS Example Creator",
            Formattime = formatTime,
            Volumeuuid = volumeuuid,
            Version = "2.4.0",
            Location = new Location { Partitions = { "a" } },
            Partitions = new Partitions { Index = "a", Data = "b" },
            Blocksize = 524288,
            Compression = true,
        };

        string xmlString = LtfsLabel.ToXml(LtfsLabel);

        if (!xmlString.Contains("encoding=\"UTF-8\""))
            Assert.Fail("XML declaration should specify UTF-8 encoding");

        if (xmlString.Contains("xmlns:xsi=") || xmlString.Contains("xmlns:xsd="))
            Assert.Fail("XML should not contain unnecessary namespace declarations");

        // verify that the XML conforms to the XSD
        var schemas = new XmlSchemaSet();
        schemas.Add("", @"..\..\..\LtfsLabel.xsd"); // specify the XSD file path

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

        // Assert
        Assert.False(hasValidationErrors); // confirm no XSD validation errors
        

        // Then verify it can be correctly deserialized
        LtfsLabel? deserialized = LtfsLabel.FromXml(xmlString);

        Assert.NotNull(deserialized);
        Assert.Equal(LtfsLabel.Creator, deserialized.Creator);
        Assert.Equal(LtfsLabel.Formattime.ToString(), deserialized.Formattime.ToString());
        Assert.Equal(LtfsLabel.Volumeuuid, deserialized.Volumeuuid);
        //Assert.Equal(LtfsLabel.Location.Partitions, deserialized.Location.Partitions);
        Assert.Equal(LtfsLabel.Partitions.Index.Value, deserialized.Partitions.Index.Value);
        Assert.Equal(LtfsLabel.Partitions.Data.Value, deserialized.Partitions.Data.Value);
        Assert.Equal(LtfsLabel.Blocksize, deserialized.Blocksize);
        Assert.Equal(LtfsLabel.Compression, deserialized.Compression);
    }

}
