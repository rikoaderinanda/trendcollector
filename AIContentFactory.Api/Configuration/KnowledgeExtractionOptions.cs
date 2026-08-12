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

    /// <summary>
    /// Delay in seconds between processing each job within a single worker
    /// cycle. Reduces the chance of triggering YouTube rate limiting (HTTP 429)
    /// when many videos are processed back-to-back.
    /// </summary>
    public int DelayBetweenJobsSeconds { get; set; } = 30;

    /// <summary>Maximum retry attempts before a job is permanently Failed.</summary>
    public int RetryCount { get; set; } = 5;

    /// <summary>
    /// Base delay in seconds for the exponential backoff applied on a failed
    /// attempt. The actual delay is 2^n * base with -25%/+25% random jitter,
    /// capped at <see cref="RetryMaxBackoffSeconds"/>.
    /// </summary>
    public int RetryBaseBackoffSeconds { get; set; } = 60;

    /// <summary>
    /// Upper bound in seconds for the exponential backoff delay between retries.
    /// </summary>
    public int RetryMaxBackoffSeconds { get; set; } = 600;

    /// <summary>
    /// Cooldown window in seconds after a transient transcript failure
    /// (e.g. HTTP 429 rate limit). When a transient failure is observed, the
    /// worker pauses processing of the remaining jobs in the batch for this
    /// duration to let YouTube's rate limiter cool down, instead of walking
    /// every remaining job into the same 429 wall.
    /// </summary>
    public int RateLimitCooldownSeconds { get; set; } = 300;

    /// <summary>AI provider type name, e.g. "OpenAICompatible".</summary>
    public string AIProvider { get; set; } = "OpenAICompatible";

    /// <summary>Base endpoint of the OpenAI-compatible API, e.g. https://api.deepseek.com.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>API key for the provider. Keep in appsettings.Local.json in development.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "deepseek-chat";

    public double Temperature { get; set; } = 0.3;

    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Maximum output tokens for the transcript-polishing call. Transcripts are
    /// often 20-50K characters (= 10-20K tokens of output) — the default
    /// <see cref="MaxTokens"/> (4096) truncates the polished JSON in the middle
    /// of the string and produces invalid JSON. This separate bound keeps the
    /// polish operation working while leaving MaxTokens for regular extraction.
    /// </summary>
    public int PolishMaxTokens { get; set; } = 16000;

    /// <summary>Version tag placed into the AI prompt for reproducibility.</summary>
    public string PromptVersion { get; set; } = "v1";

    /// <summary>When true, every saved video is automatically enqueued for knowledge extraction.</summary>
    public bool AutoEnqueueEnabled { get; set; } = true;
}