namespace AIContentFactory.Api.Configuration;

/// <summary>
/// Options for the Trend Discovery Agent.
/// Bound from the "TrendDiscovery" configuration section.
/// </summary>
public sealed class TrendDiscoveryOptions
{
    public const string SectionName = "TrendDiscovery";

    /// <summary>
    /// Selected AI provider: "NoOp" or "OpenAICompatible" (DeepSeek, OpenAI, etc.).
    /// </summary>
    public string AIProvider { get; set; } = "OpenAICompatible";

    /// <summary>Base URL of the provider API, e.g. https://api.deepseek.com</summary>
    public string Endpoint { get; set; } = "https://api.deepseek.com";

    /// <summary>Model name, e.g. "deepseek-chat".</summary>
    public string Model { get; set; } = "deepseek-chat";

    /// <summary>API key. Keep in appsettings.Local.json (gitignored) or environment variable.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Maximum number of keywords returned per run.</summary>
    public int MaxKeywordsPerRun { get; set; } = 20;

    /// <summary>AI sampling temperature.</summary>
    public double Temperature { get; set; } = 0.8;

    /// <summary>Maximum tokens for the AI response.</summary>
    public int MaxTokens { get; set; } = 2000;

    /// <summary>Niches/topics the AI should focus on.</summary>
    public List<string> Niches { get; set; } = new();

    /// <summary>Target countries.</summary>
    public List<string> Countries { get; set; } = new();

    /// <summary>Target languages.</summary>
    public List<string> Languages { get; set; } = new();
}