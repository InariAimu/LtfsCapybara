using System.Collections.Generic;
using System.Threading.Tasks;

namespace LtfsServer.Services;

public interface ILocalTapeRegistry
{
    /// <summary>
    /// Initialize the registry by scanning the given local path (AppData.Path/local).
    /// </summary>
    Task InitializeAsync(string localPath);

    /// <summary>
    /// Get all discovered tape names (directory names under local path).
    /// </summary>
    IEnumerable<string> GetTapeNames();

    /// <summary>
    /// Get CM-derived tape summaries keyed by tape name.
    /// </summary>
    IEnumerable<LocalTapeSummary> GetTapeSummaries();

    /// <summary>
    /// Get parsed file infos for a tape name.
    /// </summary>
    IEnumerable<TapeFileInfo> GetFiles(string tapeName);
}
