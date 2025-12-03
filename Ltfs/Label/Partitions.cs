using System.Xml.Serialization;
using System.Xml.Schema;

namespace Ltfs.Label;

/// <summary>
/// Represents the 'partitions' element.
/// Contains all 'index' and 'data' elements as per the all group.
/// </summary>
public class Partitions
{
    [XmlElement("index", Form = XmlSchemaForm.Unqualified)]
    public PartitionId Index { get; set; }

    [XmlElement("data", Form = XmlSchemaForm.Unqualified)]
    public PartitionId Data { get; set; }
}
