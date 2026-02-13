using Microsoft.Extensions.Logging;
using Ready.Application.Abstractions;
using Ready.Application.Results;

namespace Ready.Application.Steps;

public sealed class ExportJsonStep : IWorkflowStep
{
    private readonly ILogger<ExportJsonStep> _logger;

    public ExportJsonStep(ILogger<ExportJsonStep> logger)
    {
        _logger = logger;
    }

    public string Name => "export.json";

    public Task<StepOutcome> ExecuteAsync(StepContext context, CancellationToken ct)
    {
        _logger.LogInformation("Exporting extracted data for document {DocumentId} to JSON (stub)", context.DocumentId);
        return Task.FromResult(StepOutcome.Succeeded("Exported (stub)"));
    }
}
