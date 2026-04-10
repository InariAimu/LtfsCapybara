namespace LtfsServer.Features.Tasks;

public interface ITaskExecutionService
{
    IReadOnlyList<TaskExecutionSnapshot> ListExecutions();
    TaskExecutionSnapshot StartExecution(string tapeBarcode, string tapeDriveId);
    TaskExecutionSnapshot GetExecution(string executionId);
    TaskExecutionIncidentDto ResolveIncident(string executionId, string incidentId, string resolution);
    IAsyncEnumerable<TaskExecutionEventEnvelope> SubscribeAsync(CancellationToken cancellationToken);
}