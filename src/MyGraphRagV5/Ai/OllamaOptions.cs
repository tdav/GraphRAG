namespace MyGraphRagV5.Ai;

/// <summary>
/// Configuration for the Ollama Cloud chat client (OpenAI-compatible endpoint).
/// Bound from config section "Ollama". ApiKey must come from user-secrets or the
/// OLLAMA_API_KEY environment variable - never hardcode it.
/// </summary>
public sealed record OllamaOptions
{
    public string Endpoint { get; set; } = "https://ollama.com/v1";

    public string Model { get; set; } = "nemotron-3-super:cloud";

    public string? ApiKey { get; set; }
}
