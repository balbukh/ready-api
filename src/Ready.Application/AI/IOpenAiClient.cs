namespace Ready.Application.AI;

public interface IOpenAiClient
{
    /// <summary>
    /// Generates a JSON response from OpenAI based on instructions and user prompt.
    /// </summary>
    Task<string> GenerateJsonAsync(string instructions, string userPrompt, CancellationToken ct = default);
}
