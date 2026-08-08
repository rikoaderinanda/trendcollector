namespace AIContentFactory.Api.Configuration;

/// <summary>
/// Configuration for the knowledge extraction agent (worker + enqueue).
/// Bound from the "KnowledgeExtraction" section of appsettings.
/// </summary>
public sealed class KnowledgeExtractionOptions
{
    public const string SectionName = "KnowledgeExtraction";

    /// <summary>Whether the background worker is enabled globally.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the worker polls the queue for pending jobs.</summary>
    public int WorkerIntervalSeconds { get; set; } = 30;

    /// <summary>How many pending jobs the worker processes per cycle.</summary>
    public int BatchSize { get; set; } = 5;

    /// <summary>Maximum retry attempts before a job is permanently Failed.</summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>AI provider type name, e.g. "OpenAICompatible".</summary>
    public string AIProvider { get; set; } = "OpenAICompatible";

    /// <summary>Base endpoint of the OpenAI-compatible API, e.g. https://api.deepseek.com.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>API key for the provider. Keep in appsettings.Local.json in development.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "deepseek-chat";

    public double Temperature { get; set; } = 0.3;

    public int MaxTokens { get; set; } = 4096;

    /// <summary>Version tag placed into the AI prompt for reproducibility.</summary>
    public string PromptVersion { get; set; } = "v1";

    /// <summary>When true, every saved video is automatically enqueued for knowledge extraction.</summary>
    public bool AutoEnqueueEnabled { get; set; } = true;
}