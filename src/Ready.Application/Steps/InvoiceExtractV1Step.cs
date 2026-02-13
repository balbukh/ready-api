using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Ready.Application.Abstractions;
using Ready.Application.AI;
using Ready.Application.Results;
using Microsoft.Extensions.Logging;

namespace Ready.Application.Steps;

public sealed partial class InvoiceExtractV1Step : IWorkflowStep
{
    private readonly IOpenAiClient _ai;
    private readonly IResultStore _results;
    private readonly ILogger<InvoiceExtractV1Step> _logger;

    public string Name => "invoice.extract.v1";

    // ─── System prompt ──────────────────────────────────────────────────
    private const string SystemPrompt = """
        You are a structured-data extraction engine.
        Your ONLY job is to extract invoice data from the provided text and return it as JSON.

        Rules:
        1. Return ONLY valid JSON — no markdown, no code fences, no commentary.
        2. Unknown or missing fields MUST be null. Do NOT invent values.
        3. Dates MUST be ISO 8601 format: YYYY-MM-DD.
        4. Currency MUST be ISO 4217 (e.g. EUR, USD, GBP).
        5. All numeric values are decimals using '.' as separator.
        """;

    // ─── User prompt template ───────────────────────────────────────────
    private const string UserPromptTemplate = """
        Extract the invoice from the text below into a JSON object with exactly these keys:

        Top-level keys:
          invoiceNumber   (string | null)
          invoiceDate     (string YYYY-MM-DD | null)
          dueDate         (string YYYY-MM-DD | null)
          sellerName      (string | null)
          sellerVatId     (string | null)
          buyerName       (string | null)
          buyerVatId      (string | null)
          currency        (string ISO 4217 | null)
          subtotal        (number | null)
          vatTotal        (number | null)
          total           (number | null)
          lineItems       (array of objects | [])

        Each element in lineItems must have:
          description     (string | null)
          quantity        (number | null)
          unitPrice       (number | null)
          lineTotal       (number | null)
          vatRate         (number | null)

        Input text:
        """;

    [GeneratedRegex(@"^[A-Z]{3}$")]
    private static partial Regex CurrencyRegex();

    public InvoiceExtractV1Step(IOpenAiClient ai, IResultStore results, ILogger<InvoiceExtractV1Step> logger)
    {
        _ai = ai;
        _results = results;
        _logger = logger;
    }

    public async Task<StepOutcome> ExecuteAsync(StepContext context, CancellationToken ct)
    {
        // 1. Retrieve document text produced by the previous step (DocText)
        string text = await GetDocTextAsync(context.RunId, ct);

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("DocText payload is empty for run {RunId} doc {DocumentId}",
                context.RunId, context.DocumentId);
            return StepOutcome.Failed("No document text available for extraction.");
        }

        // 2. Build prompts
        string userPrompt = UserPromptTemplate + text;

        // 3. Call OpenAI
        string rawResponse;
        try
        {
            rawResponse = await _ai.GenerateJsonAsync(SystemPrompt, userPrompt, context.RunId, context.DocumentId, context.CorrelationId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI call failed for run {RunId} doc {DocumentId} corr {CorrelationId}",
                context.RunId, context.DocumentId, context.CorrelationId);
            return StepOutcome.Failed($"OpenAI failure: {ex.Message}");
        }

        // 4. JSON hygiene — strip whitespace + code fences
        string cleanJson = StripCodeFences(rawResponse);

        // 5. Parse JSON
        JsonNode? data;
        try
        {
            using var doc = JsonDocument.Parse(cleanJson);
            data = JsonNode.Parse(cleanJson);
            if (data is null)
            {
                await PersistErrorAsync(context, "InvalidJson", rawResponse, errors: null, ct);
                return StepOutcome.Failed("invalid json from model");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Invalid JSON from model for run {RunId} doc {DocumentId} corr {CorrelationId}: {Raw}",
                context.RunId, context.DocumentId, context.CorrelationId, cleanJson);

            await PersistErrorAsync(context, "InvalidJson", rawResponse, errors: null, ct);
            return StepOutcome.Failed("invalid json from model");
        }

        // 6. Validate required fields
        var errors = ValidateFields(data);
        if (errors.Count > 0)
        {
            _logger.LogWarning(
                "Validation failed for run {RunId} doc {DocumentId} corr {CorrelationId}: {Errors}",
                context.RunId, context.DocumentId, context.CorrelationId,
                string.Join("; ", errors));

            await PersistErrorAsync(context, "ValidationFailed", data.ToJsonString(), errors, ct);
            return StepOutcome.Failed($"validation failed: {string.Join(", ", errors)}");
        }

        // 7. Re-serialize as compact JSON and persist
        string compactJson = data.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        var result = new ResultEnvelope("InvoiceExtract", "v1", JsonNode.Parse(compactJson)!);
        return StepOutcome.Succeeded(result: result);
    }

    // ─── Validation ─────────────────────────────────────────────────────

    private static List<string> ValidateFields(JsonNode data)
    {
        var errors = new List<string>();

        // total must exist and be a number > 0
        var totalNode = data["total"];
        if (totalNode is null || totalNode.GetValueKind() == JsonValueKind.Null)
            errors.Add("total is missing");
        else if (!TryGetDouble(totalNode, out var totalVal) || totalVal <= 0)
            errors.Add("total must be a number > 0");

        // currency must exist and be 3-letter uppercase A-Z
        var currencyNode = data["currency"];
        if (currencyNode is null || currencyNode.GetValueKind() == JsonValueKind.Null)
            errors.Add("currency is missing");
        else
        {
            var currStr = currencyNode.ToString();
            if (!CurrencyRegex().IsMatch(currStr))
                errors.Add($"currency must be 3-letter uppercase (got '{currStr}')");
        }

        // Identity: invoiceNumber non-empty OR (invoiceDate non-empty AND sellerName non-empty)
        bool hasInvoiceNumber = IsNonEmptyString(data["invoiceNumber"]);
        bool hasInvoiceDate = IsNonEmptyString(data["invoiceDate"]);
        bool hasSellerName = IsNonEmptyString(data["sellerName"]);

        if (!hasInvoiceNumber && !(hasInvoiceDate && hasSellerName))
            errors.Add("must have invoiceNumber, or both invoiceDate and sellerName");

        // lineItems if present must be an array
        var lineItemsNode = data["lineItems"];
        if (lineItemsNode is not null
            && lineItemsNode.GetValueKind() != JsonValueKind.Null
            && lineItemsNode is not JsonArray)
        {
            errors.Add("lineItems must be an array");
        }

        return errors;
    }

    private static bool IsNonEmptyString(JsonNode? node)
    {
        if (node is null || node.GetValueKind() == JsonValueKind.Null)
            return false;
        return !string.IsNullOrWhiteSpace(node.ToString());
    }

    private static bool TryGetDouble(JsonNode node, out double value)
    {
        value = 0;
        try
        {
            value = node.GetValue<double>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ─── Error persistence ──────────────────────────────────────────────

    private async Task PersistErrorAsync(
        StepContext context,
        string reason,
        string raw,
        List<string>? errors,
        CancellationToken ct)
    {
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["reason"] = reason,
                ["raw"] = raw,
                ["documentId"] = context.DocumentId.ToString(),
                ["runId"] = context.RunId.ToString(),
                ["step"] = Name,
            };
            if (errors is not null)
                payload["errors"] = errors;

            await _results.SaveAsync(context.RunId, "InvoiceExtractError", "v1", payload, ct);

            _logger.LogInformation(
                "Persisted InvoiceExtractError for run {RunId} doc {DocumentId} reason={Reason}",
                context.RunId, context.DocumentId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to persist InvoiceExtractError for run {RunId} doc {DocumentId}",
                context.RunId, context.DocumentId);
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private async Task<string> GetDocTextAsync(Guid runId, CancellationToken ct)
    {
        var runResults = await _results.GetResultsAsync(runId, ct);
        var docTextResult = runResults.FirstOrDefault(r => r.ResultType == "DocText");

        if (docTextResult?.Payload is null)
            return string.Empty;

        return ExtractTextFromPayload(docTextResult.Payload);
    }

    private static string ExtractTextFromPayload(object payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("text", out var textProp))
                return textProp.GetString() ?? string.Empty;
        }
        catch
        {
            // Swallow — best-effort extraction
        }
        return string.Empty;
    }

    private static string StripCodeFences(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        // Strip leading/trailing whitespace
        var trimmed = raw.Trim();

        // Remove leading ```json (or ```) and trailing ```
        trimmed = Regex.Replace(trimmed, @"^```(?:json)?\s*\n?", "", RegexOptions.IgnoreCase);
        trimmed = Regex.Replace(trimmed, @"\n?```\s*$", "");
        return trimmed.Trim();
    }
}
