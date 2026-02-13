using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ready.Application.AI;

public sealed class OpenAiClient : IOpenAiClient
{
    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiClient> _logger;

    public OpenAiClient(HttpClient http, IOptions<OpenAiOptions> options, ILogger<OpenAiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GenerateJsonAsync(
        string instructions,
        string userPrompt,
        Guid? runId = null,
        Guid? documentId = null,
        Guid? correlationId = null,
        CancellationToken ct = default)
    {
        // Enforce hardened request fields
        var request = new
        {
            model = _options.Model,
            instructions,
            input = userPrompt,
            text = new { format = new { type = "json_object" } },
            max_output_tokens = 1000 // Reasonable cap
        };

        var delay = TimeSpan.FromSeconds(1);
        const int maxAttempts = 3;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "OpenAI Attempt {Attempt}/{MaxAttempts} for Run {RunId} Doc {DocumentId} Corr {CorrelationId}",
                    attempt, maxAttempts, runId, documentId, correlationId);

                using var response = await _http.PostAsJsonAsync("https://api.openai.com/v1/responses", request, ct);

                if (response.IsSuccessStatusCode)
                {
                    // Success! Extract and return.
                    var json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
                    return ExtractContent(json);
                }

                // Handle Transient Failures (429, 5xx)
                if (attempt < maxAttempts && IsTransient(response.StatusCode))
                {
                    var retryAfter = GetRetryAfter(response);
                    var currentDelay = retryAfter ?? delay;

                    // Cap at 10s if we got a huge Retry-After
                    if (currentDelay.TotalSeconds > 10) currentDelay = TimeSpan.FromSeconds(10);

                    // Add jitter (0-500ms)
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
                    var waitTime = currentDelay + jitter;

                    _logger.LogWarning(
                        "OpenAI Transient Failure {StatusCode} on Attempt {Attempt}. Waiting {WaitTime}s before retry. Run {RunId}",
                        response.StatusCode, attempt, waitTime.TotalSeconds, runId);

                    await Task.Delay(waitTime, ct);

                    // Exponential backoff for next time (unless we used Retry-After, but increasing baseline is safe)
                    delay *= 2;
                    continue;
                }

                // If not transient or retries exhausted, log error and throw
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "OpenAI API error {StatusCode} on Attempt {Attempt}: {Body}. Run {RunId}",
                    (int)response.StatusCode, attempt, errorBody, runId);
                
                response.EnsureSuccessStatusCode(); // throw
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts && (ex.StatusCode == null || IsTransient(ex.StatusCode.Value)))
            {
                 // Network errors are also transient
                 _logger.LogWarning(ex, "OpenAI Network Error on Attempt {Attempt}. Retrying...", attempt);
                 await Task.Delay(delay, ct);
                 delay *= 2;
            }
        }

        throw new InvalidOperationException("OpenAI retries exhausted");
    }

    private string ExtractContent(JsonNode? json)
    {
        if (json is null) return string.Empty;

        // Responses API: output[] may contain reasoning + message entries.
        // Find the "message" type entry and extract output_text content.
        if (json["output"] is JsonArray outputArray)
        {
            foreach (var item in outputArray)
            {
                if (item?["type"]?.ToString() == "message")
                {
                    var text = item["content"]?[0]?["text"];
                    if (text is not null) return text.ToString();
                }
            }
        }

        // Fallback: Chat Completions format
        var content = json["choices"]?[0]?["message"]?["content"];
        if (content is not null) return content.ToString();

        // Last resort
        _logger.LogWarning("Could not extract content from OpenAI response");
        return json.ToJsonString();
    }

    private static bool IsTransient(HttpStatusCode code)
    {
        // 429 Too Many Requests
        // 5xx Server Errors
        return code == HttpStatusCode.TooManyRequests || 
               (int)code >= 500;
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header is null) return null;

        if (header.Delta.HasValue) return header.Delta.Value;
        if (header.Date.HasValue) return header.Date.Value - DateTimeOffset.UtcNow;

        return null;
    }
}
