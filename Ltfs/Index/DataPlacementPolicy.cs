using System.Xml.Serialization;
using System.Xml.Schema;

namespace Ltfs.Index;

[Serializable()]
[System.Diagnostics.DebuggerStepThrough()]
[System.ComponentModel.DesignerCategory("code")]
public partial class DataPlacementPolicy
{
    [XmlElement("indexpartitioncriteria", Form = XmlSchemaForm.Unqualified)]
    public required IndexPartitionCriteria IndexPartitionCriteria { get; set; }
}
