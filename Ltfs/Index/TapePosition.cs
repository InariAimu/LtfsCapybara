using System.Xml.Serialization;
using System.Xml.Schema;

namespace Ltfs.Index;

[Serializable()]
[System.Diagnostics.DebuggerStepThrough()]
[System.ComponentModel.DesignerCategory("code")]
public partial class TapePosition
{
    string _partition = "a";

    [XmlElement("partition", Form = XmlSchemaForm.Unqualified)]
    public string Partition
    {
        get => _partition;
        set
        {
            if (_partition == string.Empty || (value != "a" && value != "b"))
            {
                throw new ArgumentException("Partition must be 'a' or 'b'");
            }
            _partition = value;
        }
    }


    [XmlElement("startblock", Form = XmlSchemaForm.Unqualified)]
    public uint StartBlock { get; set; }
}
