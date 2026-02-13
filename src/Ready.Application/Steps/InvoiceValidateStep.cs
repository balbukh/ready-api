using Ready.Application.Abstractions;
using Ready.Application.Results;
using Ready.Domain.Results;

namespace Ready.Application.Steps;

public sealed class InvoiceValidateStep : IWorkflowStep
{
    public string Name => "invoice.validate.v1";

    public Task<StepOutcome> ExecuteAsync(StepContext context, CancellationToken ct)
    {
        if (!context.Items.TryGetValue("InvoiceExtract", out var extractObj) || extractObj is not InvoiceExtractV1 extract)
        {
            return Task.FromResult(StepOutcome.Failed("No InvoiceExtract found in context"));
        }

        var issues = new List<string>();
        
        if (extract.TotalInclVat <= 0)
            issues.Add("Total amount must be positive");
            
        if (extract.InvoiceDate == default)
            issues.Add("Invoice date is missing");

        var report = new ValidationReport(issues.Count == 0, issues);

        var result = new ResultEnvelope<ValidationReport>(
            ValidationReport.ResultType,
            ValidationReport.Version,
            report);

        var msg = report.IsValid ? "Validation passed" : $"Validation failed with {issues.Count} issues";
        return Task.FromResult(StepOutcome.Succeeded(msg, result));
    }
}
