namespace Ready.Domain.Runs;

public sealed class DocumentRun
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid DocumentId { get; init; }
    public string WorkflowName { get; init; } = default!;
    public string WorkflowVersion { get; init; } = "v1";
    public RunStatus Status { get; set; } = RunStatus.Created;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}