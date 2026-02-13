using Ready.Application.Abstractions;
using Ready.Application.Results;
using Ready.Domain.Results;

namespace Ready.Application.Steps;

public sealed class InvoiceExtractStep : IWorkflowStep
{
    public string Name => "invoice.extract.v1";

    public Task<StepOutcome> ExecuteAsync(StepContext context, CancellationToken ct)
    {
        if (!context.Items.TryGetValue("ExtractedText", out var textObj) || textObj is not string text)
        {
            return Task.FromResult(StepOutcome.Failed("No ExtractedText found in context"));
        }

        // Stub: In real life this would use an LLM
        var extract = new InvoiceExtractV1(
            InvoiceNumber: "INV-2024-001",
            InvoiceDate: new DateOnly(2024, 2, 12),
            DueDate: new DateOnly(2024, 3, 12),
            Currency: "EUR",
            TotalExclVat: 100.00m,
            VatAmount: 21.00m,
            TotalInclVat: 121.00m,
            VatRateGuess: 21.0m,
            Supplier: new SupplierInfo("Acme Corp", "NL", "NL123456789B01", "12345678", "NL99BANK0123456789"),
            Buyer: new BuyerInfo(context.CustomerId, null),
            Quality: new QualityInfo(0.95, null),
            Issues: Array.Empty<ExtractionIssue>(),
            Evidence: Array.Empty<FieldEvidence>()
        );

        context.Items["InvoiceExtract"] = extract;

        // Return wrapping envelope
        var result = new ResultEnvelope<InvoiceExtractV1>(
            InvoiceExtractV1.ResultType, 
            InvoiceExtractV1.Version, 
            extract);

        return Task.FromResult(StepOutcome.Succeeded("Invoice extracted (stub)", result));
    }
}
