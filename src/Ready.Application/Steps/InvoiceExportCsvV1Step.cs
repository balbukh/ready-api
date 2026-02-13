using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Ready.Application.Abstractions;
using Ready.Application.Results;

namespace Ready.Application.Steps;

public sealed class InvoiceExportCsvV1Step : IWorkflowStep
{
    private readonly IResultStore _results;
    private readonly ILogger<InvoiceExportCsvV1Step> _logger;

    public string Name => "invoice.export.csv.v1";

    public InvoiceExportCsvV1Step(IResultStore results, ILogger<InvoiceExportCsvV1Step> logger)
    {
        _results = results;
        _logger = logger;
    }

    public async Task<StepOutcome> ExecuteAsync(StepContext context, CancellationToken ct)
    {
        // 1. Get InvoiceExtract result
        var runResults = await _results.GetResultsAsync(context.RunId, ct);
        var extractResult = runResults.FirstOrDefault(r => 
            r.ResultType == "InvoiceExtract" && r.Version == "v1");

        if (extractResult?.Payload is null)
        {
            _logger.LogWarning("No InvoiceExtract v1 result found for run {RunId}", context.RunId);
            return StepOutcome.Failed("Missing InvoiceExtract result");
        }

        // 2. Parse payload
        JsonNode? invoiceData;
        try 
        {
            var json = JsonSerializer.Serialize(extractResult.Payload);
            invoiceData = JsonNode.Parse(json);
        }
        catch (Exception ex)
        {
            return StepOutcome.Failed($"Failed to parse invoice data: {ex.Message}");
        }

        if (invoiceData is null) return StepOutcome.Failed("Invoice data is null");

        // 3. Build CSV
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine("invoiceNumber,invoiceDate,dueDate,sellerName,sellerVatId,buyerName,buyerVatId,currency,subtotal,vatTotal,total");

        // Invoice Row
        sb.Append(Escape(invoiceData["invoiceNumber"]));
        sb.Append(',');
        sb.Append(Escape(invoiceData["invoiceDate"]));
        sb.Append(',');
        sb.Append(Escape(invoiceData["dueDate"]));
        sb.Append(',');
        sb.Append(Escape(invoiceData["sellerName"]));
        sb.Append(',');
        sb.Append(Escape(invoiceData["sellerVatId"]));
        sb.Append(',');
        sb.Append(Escape(invoiceData["buyerName"]));
        sb.Append(',');
        sb.Append(Escape(invoiceData["buyerVatId"]));
        sb.Append(',');
        sb.Append(Escape(invoiceData["currency"]));
        sb.Append(',');
        sb.Append(FormatNumber(invoiceData["subtotal"]));
        sb.Append(',');
        sb.Append(FormatNumber(invoiceData["vatTotal"]));
        sb.Append(',');
        sb.AppendLine(FormatNumber(invoiceData["total"]));

        sb.AppendLine(); // Blank line

        // Line Items Header
        sb.AppendLine("description,quantity,unitPrice,lineTotal,vatRate");

        // Line Items Rows
        if (invoiceData["lineItems"] is JsonArray items)
        {
            foreach (var item in items)
            {
                if (item is null) continue;
                sb.Append(Escape(item["description"]));
                sb.Append(',');
                sb.Append(FormatNumber(item["quantity"]));
                sb.Append(',');
                sb.Append(FormatNumber(item["unitPrice"]));
                sb.Append(',');
                sb.Append(FormatNumber(item["lineTotal"]));
                sb.Append(',');
                sb.AppendLine(FormatNumber(item["vatRate"]));
            }
        }

        var csvContent = sb.ToString();
        var bytes = Encoding.UTF8.GetBytes(csvContent);
        var base64 = Convert.ToBase64String(bytes);

        // 4. Save Result
        var payload = new
        {
            fileName = $"invoice_{context.DocumentId}.csv",
            contentType = "text/csv",
            csvBase64 = base64
        };

        var resultEnvelope = new ResultEnvelope("InvoiceCsv", "v1", payload);
        return StepOutcome.Succeeded(result: resultEnvelope);
    }

    private static string Escape(JsonNode? node)
    {
        if (node is null) return "";
        var val = node.ToString();
        if (string.IsNullOrEmpty(val)) return "";

        if (val.Contains('"') || val.Contains(',') || val.Contains('\n') || val.Contains('\r'))
        {
            return $"\"{val.Replace("\"", "\"\"")}\"";
        }
        return val;
    }

    private static string FormatNumber(JsonNode? node)
    {
        if (node is null) return "";
        return node.ToString(); // JSON numbers stringify correctly usually
    }
}
