using Ready.Application.Abstractions;

namespace Ready.Application.Steps;

public sealed class EchoStep : IWorkflowStep
{
    public string Name => "echo";

    public Task<StepOutcome> ExecuteAsync(StepContext context, CancellationToken ct)
    {
        var payload = new { ok = true, at = DateTimeOffset.UtcNow, doc = context.DocumentId };
        return Task.FromResult(new StepOutcome(StepOutcomeStatus.Succeeded, "echo ok", Result: payload));
    }
}