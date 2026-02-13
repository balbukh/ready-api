using Ready.Application.DTOs;

namespace Ready.Application.Results;

public interface IResultStore
{
    Task SaveAsync(Guid runId, string resultType, string version, object payload, CancellationToken ct);
    Task<IReadOnlyList<WorkflowResultDto>> GetResultsAsync(Guid runId, CancellationToken ct);
}