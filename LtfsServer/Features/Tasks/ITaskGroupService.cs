using Ltfs;
using Ltfs.Index;
using Ltfs.Tasks;

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
    void CompleteTasks(string tapeBarcode, IEnumerable<string> taskIds);

    /// <summary>
    /// Smart-delete a path from the local index:
    /// removes all existing tasks for the path and its descendants,
    /// then adds individual delete tasks for every item that exists in
    /// <paramref name="dirAtPath"/> (the already-resolved filesystem directory).
    /// </summary>
    TapeFsTaskGroup DeleteLocalIndexPath(string tapeBarcode, string tapePath, LtfsDirectory? dirAtPath);
}
