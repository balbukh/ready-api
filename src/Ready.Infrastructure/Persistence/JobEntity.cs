namespace Ready.Infrastructure.Persistence;

public sealed class JobEntity
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string WorkflowName { get; set; } = default!;
    public string WorkflowVersion { get; set; } = default!;
    public int Status { get; set; }           // 0=Pending,1=Running,2=Done,3=Failed
    public int Attempts { get; set; }
    public DateTimeOffset NextRunAt { get; set; }
    public string? LastError { get; set; }
    public string? ParamsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}