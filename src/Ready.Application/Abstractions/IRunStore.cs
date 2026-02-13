using Ready.Application.DTOs;

namespace Ready.Application.Abstractions;

public interface IRunStore
{
    Task<Guid> CreateRunAsync(Guid documentId, string workflowName, string workflowVersion, CancellationToken ct);
    Task MarkRunSucceededAsync(Guid runId, CancellationToken ct);
    Task MarkRunFailedAsync(Guid runId, string error, CancellationToken ct);

    Task<Guid> CreateStepRunAsync(Guid runId, string stepName, CancellationToken ct);
    Task MarkStepSucceededAsync(Guid stepRunId, CancellationToken ct);
    Task MarkStepFailedAsync(Guid stepRunId, string diagnosticsJson, CancellationToken ct);

    Task<IReadOnlyList<WorkflowRunDto>> GetRunsAsync(Guid documentId, CancellationToken ct);
}