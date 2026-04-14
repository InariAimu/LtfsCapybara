namespace LtfsServer.Features.Tasks;

public static class TapeFsTaskType
{
    public const string Add = "add";
    public const string Rename = "rename";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string Read = "read";
    public const string Verify = "verify";
    public const string Format = "format";

    public static bool IsValid(string type)
    {
        return type is Add or Rename or Update or Delete or Read or Verify or Format;
    }
}

public static class TapeFsLegacyTaskType
{
    public const string Write = "write";
    public const string Replace = "replace";
    public const string Folder = "folder";
}
