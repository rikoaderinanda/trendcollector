namespace AIContentFactory.Api.Configuration;

/// <summary>
/// Configuration for the Viral Analyzer agent (worker, AI provider and
/// analysis limits). Bound from the "ViralAnalysis" section of appsettings.
/// </summary>
public sealed class ViralAnalysisOptions
{
    public const string SectionName = "ViralAnalysis";

    /// <summary>Whether the background worker is enabled globally.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the worker checks if a daily analysis should run.</summary>
    public int WorkerIntervalMinutes { get; set; } = 360;

    /// <summary>AI provider type name, e.g. "OpenAICompatible".</summary>
    public string AIProvider { get; set; } = "OpenAICompatible";

    /// <summary>Base endpoint of the OpenAI-compatible API, e.g. https://api.deepseek.com.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>API key for the provider. Keep in appsettings.Local.json in development.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "deepseek-chat";

    public double Temperature { get; set; } = 0.4;

    public int MaxTokens { get; set; } = 8000;

    /// <summary>Maximum number of candidate videos to include in one analysis.</summary>
    public int MaxVideosPerAnalysis { get; set; } = 50;

    /// <summary>Maximum transcript characters sent to the LLM per video.</summary>
    public int MaxTranscriptCharacters { get; set; } = 10_000;

    /// <summary>How many candidates to pass to the AI in one batch.</summary>
    public int BatchSize { get; set; } = 25;

    /// <summary>Version tag placed into the AI prompt for reproducibility.</summary>
    public string PromptVersion { get; set; } = "v1";

    /// <summary>Number of content opportunities to generate (default 5).</summary>
    public int OpportunityCount { get; set; } = 5;

    /// <summary>Minimum momentum/growth score a video needs to be a strong candidate (0-100).</summary>
    public decimal MinimumMomentumScore { get; set; } = 0;

    /// <summary>How many days of collected videos to consider for the daily analysis.</summary>
    public int LookbackDays { get; set; } = 3;
}