namespace Ready.Infrastructure.Persistence;

public sealed class StepRunEntity
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string StepName { get; set; } = default!;
    public int Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? DiagnosticsJson { get; set; }
}