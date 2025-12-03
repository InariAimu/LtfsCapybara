using System.Xml.Serialization;
using System.Xml.Schema;
using Ltfs.Utils;

namespace Ltfs.Index;

[Serializable()]
[System.Diagnostics.DebuggerStepThrough()]
[System.ComponentModel.DesignerCategory("code")]
[XmlType(AnonymousType = true)]
[XmlRoot(Namespace = "", IsNullable = false)]
public partial class LtfsDirectory
{
    [XmlElement("fileuid", Form = XmlSchemaForm.Unqualified)]
    public required uint FileUID { get; set; }


    [XmlElement("name", Form = XmlSchemaForm.Unqualified)]
    public required NameType Name { get; set; }


    [XmlElement("creationtime", Form = XmlSchemaForm.Unqualified)]
    public required XDateTime CreationTime { get; set; }


    [XmlElement("changetime", Form = XmlSchemaForm.Unqualified)]
    public required XDateTime ChangeTime { get; set; }


    [XmlElement("modifytime", Form = XmlSchemaForm.Unqualified)]
    public required XDateTime ModifyTime { get; set; }


    [XmlElement("accesstime", Form = XmlSchemaForm.Unqualified)]
    public required XDateTime AccessTime { get; set; }


    [XmlElement("backuptime", Form = XmlSchemaForm.Unqualified)]
    public required XDateTime BackupTime { get; set; }


    [XmlElement("readonly", Form = XmlSchemaForm.Unqualified)]
    public required bool ReadOnly { get; set; } = false;


    [XmlElement("extendedattributes", Form = XmlSchemaForm.Unqualified, IsNullable = true)]
    public ExtendedAttributes? ExtendedAttributes { get; set; }
    public bool ShouldSerializeExtendedAttributes() => ExtendedAttributes is not null && ExtendedAttributes.Xattrs.Length > 0;


    [XmlArray("contents", Form = XmlSchemaForm.Unqualified)]
    [XmlArrayItem("directory", typeof(LtfsDirectory), IsNullable = false)]
    [XmlArrayItem("file", typeof(LtfsFile), IsNullable = false)]
    public required object[] Contents
    {
        get => _contents.ToArray();
        set => _contents = value.ToList();
    }

    private List<object> _contents = new();

    public int Count => _contents.Count;

    public override string ToString() => Name;


    public static LtfsDirectory Default()
    {
        return new LtfsDirectory
        {
            Name = new NameType { Value = "/" },
            FileUID = 0,
            CreationTime = DateTime.UtcNow,
            ChangeTime = DateTime.UtcNow,
            ModifyTime = DateTime.UtcNow,
            AccessTime = DateTime.UtcNow,
            BackupTime = DateTime.UtcNow,
            ReadOnly = false,
            Contents = Array.Empty<object>()
        };
    }

    public object? this[string name]
    {
        get
        {
            foreach (var item in _contents)
            {
                if (item is LtfsFile file && file.Name.GetName() == name)
                    return file;
                if (item is LtfsDirectory dir && dir.Name.GetName() == name)
                    return dir;
            }
            return null;
        }
        set
        {
            if (value is null || !(value is LtfsFile or LtfsDirectory))
                throw new ArgumentException("Value must be of type LtfsFile or LtfsDirectory.", nameof(value));

            bool found = false;
            for (int i = 0; i < _contents.Count; i++)
            {
                if (_contents[i] is LtfsFile file && file.Name.GetName() == name)
                {
                    _contents[i] = value;
                    found = true;
                    break;
                }
                if (_contents[i] is LtfsDirectory dir && dir.Name.GetName() == name)
                {
                    _contents[i] = value;
                    found = true;
                    break;
                }
            }
            if (found)
                return;

            _contents.Add(value);
        }
    }


    public void RemoveAll()
    {
        _contents.Clear();
    }

    
    // enumerate all files
    public IEnumerable<LtfsFile> EnumerateFiles(bool recursive = false)
    {
        foreach (var item in _contents)
        {
            if (item is LtfsFile file)
            {
                yield return file;
            }
            else if (recursive && item is LtfsDirectory dir)
            {
                foreach (var subFile in dir.EnumerateFiles(true))
                {
                    yield return subFile;
                }
            }
        }
    }

    // enumerate all directories
    public IEnumerable<LtfsDirectory> EnumerateDirectories(bool recursive = false)
    {
        foreach (var item in _contents)
        {
            if (item is LtfsDirectory dir)
            {
                yield return dir;

                if (recursive)
                {
                    foreach (var subDir in dir.EnumerateDirectories(true))
                    {
                        yield return subDir;
                    }
                }
            }
        }
    }

}
