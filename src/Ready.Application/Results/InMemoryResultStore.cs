using Ready.Application.DTOs;

namespace Ready.Application.Results;

public sealed class InMemoryResultStore : IResultStore
{
    // Changed to public property for testing
    public List<(Guid RunId, string ResultType, string Version, object Payload)> Results { get; } = new();

    public Task SaveAsync(Guid runId, string resultType, string version, object payload, CancellationToken ct)
    {
        Results.Add((runId, resultType, version, payload));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkflowResultDto>> GetResultsAsync(Guid runId, CancellationToken ct)
    {
        var list = Results
            .Where(r => r.RunId == runId)
            // Note: In real app Payload is object, here we cast/wrap it. 
            // DTO expects object Payload, so it's fine.
            .Select(r => new WorkflowResultDto(r.ResultType, r.Version, r.Payload, DateTimeOffset.UtcNow))
            .ToList();
            
        return Task.FromResult<IReadOnlyList<WorkflowResultDto>>(list);
    }
}