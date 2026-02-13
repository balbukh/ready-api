namespace Ready.Domain.Runs;

public sealed class StepRun
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RunId { get; init; }
    public string StepName { get; init; } = default!;
    public StepStatus Status { get; set; } = StepStatus.Created;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? DiagnosticsJson { get; set; }
}