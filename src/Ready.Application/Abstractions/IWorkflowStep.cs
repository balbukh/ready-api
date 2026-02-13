namespace Ready.Application.Abstractions;

public interface IWorkflowStep
{
    string Name { get; }

    Task<StepOutcome> ExecuteAsync(StepContext context, CancellationToken ct);
}