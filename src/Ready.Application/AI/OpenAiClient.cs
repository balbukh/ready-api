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

    public async Task<string> GenerateJsonAsync(string instructions, string userPrompt, CancellationToken ct = default)
    {
        // OpenAI Responses API format:
        // - input: string (simple text) or array of message objects
        // - instructions: system-level instructions
        // - text.format: { type: "json_object" } for JSON mode
        var request = new
        {
            model = _options.Model,
            instructions,
            input = userPrompt,
            text = new { format = new { type = "json_object" } }
        };

        var response = await _http.PostAsJsonAsync("https://api.openai.com/v1/responses", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("OpenAI API error {StatusCode}: {Body}", (int)response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode(); // throw
        }

        var json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);

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

        // Last resort: return the whole response
        _logger.LogWarning("Could not extract content from OpenAI response, returning full body");
        return json.ToJsonString();
    }
}
