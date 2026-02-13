using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Ready.Application.Abstractions;
using Ready.Application.AI;
using Ready.Application.Results;
using Microsoft.Extensions.Logging;

namespace Ready.Application.Steps;

public sealed class InvoiceExtractV1Step : IWorkflowStep
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
            _logger.LogWarning("DocText payload is empty for run {RunId}. Returning failed.", context.RunId);
            return StepOutcome.Failed("No document text available for extraction.");
        }

        // 2. Build prompts
        string userPrompt = UserPromptTemplate + text;

        // 3. Call OpenAI
        string rawResponse;
        try
        {
            rawResponse = await _ai.GenerateJsonAsync(SystemPrompt, userPrompt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI call failed for run {RunId}", context.RunId);
            return StepOutcome.Failed($"OpenAI failure: {ex.Message}");
        }

        // 4. JSON hygiene — strip ```json fences if present
        string cleanJson = StripCodeFences(rawResponse);

        // 5. Validate JSON parse
        JsonNode? data;
        try
        {
            data = JsonNode.Parse(cleanJson);
            if (data is null)
                return StepOutcome.Failed("AI returned empty JSON.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON from AI for run {RunId}: {Raw}", context.RunId, cleanJson);
            return StepOutcome.Failed($"Invalid JSON from AI: {ex.Message}");
        }

        // 6. Re-serialize as compact JSON for storage
        string compactJson = data.ToJsonString(new JsonSerializerOptions { WriteIndented = false });

        // 7. Wrap in ResultEnvelope
        var result = new ResultEnvelope("InvoiceExtract", "v1", JsonNode.Parse(compactJson)!);
        return StepOutcome.Succeeded(result: result);
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

        // Remove leading ```json (or ```) and trailing ```
        var trimmed = raw.Trim();
        trimmed = Regex.Replace(trimmed, @"^```(?:json)?\s*\n?", "", RegexOptions.IgnoreCase);
        trimmed = Regex.Replace(trimmed, @"\n?```\s*$", "");
        return trimmed.Trim();
    }
}
