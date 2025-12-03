using System.Xml.Serialization;
using System.Xml.Schema;

namespace Ltfs.Label;

public class Location
{
    private List<PartitionId> _partitions = new List<PartitionId>();


    [XmlElement("partition", Form = XmlSchemaForm.Unqualified)]
    public List<PartitionId> Partitions
    {
        get => _partitions;
        set => _partitions = value;
    }
}
