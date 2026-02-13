using Ready.Application.Abstractions;
using Ready.Application.Results;

namespace Ready.Application.Steps;

public sealed class TextExtractStep : IWorkflowStep
{
    public string Name => "text.extract";

    public Task<StepOutcome> ExecuteAsync(StepContext context, CancellationToken ct)
    {
        // Stub: In real life this would call Azure AI Document Intelligence or Amazon Textract
        var text = "INVOICE #INV-2024-001\nDate: 2024-02-12\nTotal: 121.00 EUR";
        
        context.Items["ExtractedText"] = text;

        return Task.FromResult(StepOutcome.Succeeded("Text extracted (stub)"));
    }
}
