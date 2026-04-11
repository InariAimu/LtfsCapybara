namespace LtfsServer.Features.Tasks;

public interface ITaskExecutionService
{
    IReadOnlyList<TaskExecutionSnapshot> ListExecutions();
    TaskExecutionSnapshot StartExecution(string tapeBarcode, string tapeDriveId, bool scsiMetricsEnabled);
    TaskExecutionSnapshot GetExecution(string executionId);
    TaskExecutionSnapshot UpdateScsiMetrics(string executionId, bool enabled);
    TaskExecutionIncidentDto ResolveIncident(string executionId, string incidentId, string resolution);
    IAsyncEnumerable<TaskExecutionEventEnvelope> SubscribeAsync(CancellationToken cancellationToken);
}