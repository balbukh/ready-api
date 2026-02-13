namespace Ready.Application.Abstractions;

public sealed record EnqueueJobRequest(
    Guid DocumentId,
    string WorkflowName,
    string WorkflowVersion,
    Dictionary<string, string>? Params = null
);

public interface IJobQueue
{
    Task EnqueueAsync(EnqueueJobRequest request, CancellationToken ct);
    Task<JobLease?> TryDequeueAsync(TimeSpan leaseTime, CancellationToken ct);
    Task MarkSucceededAsync(Guid jobId, CancellationToken ct);
    Task MarkFailedAsync(Guid jobId, string error, TimeSpan? retryAfter, CancellationToken ct);
}

public sealed record JobLease(
    Guid JobId,
    Guid DocumentId,
    string WorkflowName,
    string WorkflowVersion,
    int Attempts,
    Dictionary<string, string> Params
);