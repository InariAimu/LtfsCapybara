using Ltfs;

namespace LtfsServer.Features.Tasks;

public interface ITaskGroupService
{
    IReadOnlyList<TapeFsTaskGroup> ListGroups();
    TapeFsTaskGroup GetOrCreateGroup(string tapeBarcode);
    TapeFsTaskGroup RenameGroup(string tapeBarcode, string name);
    TapeFsTaskGroup AddTask(string tapeBarcode, TapeFsTaskCreateRequest request);
    TapeFsTaskGroup AddServerFolderTask(string tapeBarcode, AddTapeFsServerFolderTaskRequest request);
    TapeFsTaskGroup AddFormatTask(string tapeBarcode, FormatTask? formatTask = null);
    TapeFsTaskGroup DeleteTask(string tapeBarcode, string taskId);
}
