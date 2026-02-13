namespace Ready.Infrastructure.Persistence;

public sealed class RunEntity
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string WorkflowName { get; set; } = default!;
    public string WorkflowVersion { get; set; } = default!;
    public int Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}