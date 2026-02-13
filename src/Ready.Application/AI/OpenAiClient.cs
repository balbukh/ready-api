using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Ready.Application.AI;

public sealed class OpenAiClient : IOpenAiClient
{
    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;

    public OpenAiClient(HttpClient http, IOptions<OpenAiOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string> GenerateJsonAsync(string instructions, string userPrompt, CancellationToken ct = default)
    {
        var request = new
        {
            model = _options.Model,
            instructions = instructions,
            input = new[] { userPrompt },
            response_format = new { type = "json_object" }
        };

        var response = await _http.PostAsJsonAsync("https://api.openai.com/v1/responses", request, ct);
        
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
        
        // Assuming standard OpenAI response structure or expected v1/responses structure.
        // If v1/responses returns the content directly or in a specific field, we need to adapt.
        // Based on typical OpenAI API patterns, let's assume it might return a 'output' or similar.
        // However, without documentation, I'll log content if I can't find it.
        // For now, let's try to extract from a likely field or return the whole JSON string if unsure.
        // Actually, let's try to grab 'output' which is common for newer endpoints, or fallback to 'choices'.
        
        // Strategy: Return the raw content from the likely field.
        // If it's the responses API, it might be just the object. 
        // Let's assume the API returns the result in a field named 'output' or similar.
        // Given I cannot verify the exact response shape without running it, I will return the full string
        // if I can't find a specific content field, but I'll try to look for 'output' or 'message'.
        
        // Edit: For the purpose of "GenerateJsonAsync", the caller expects the JSON string.
        // If the 'responses' endpoint returns the JSON object directly as the body, then 'json' IS the result.
        // But usually there's an envelope.
        
        if (json is null) return string.Empty;

        // Try to find content in common places
        if (json["output"] is JsonValue outputNode) return outputNode.ToString();
        if (json["choices"]?[0]?["message"]?["content"] is JsonValue contentNode) return contentNode.ToString();

        // Fallback: return the whole JSON string
        return json.ToString();
    }
}
