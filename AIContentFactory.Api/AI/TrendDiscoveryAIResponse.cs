namespace AIContentFactory.Api.AI;

/// <summary>
/// Result of a trend discovery AI call.
/// </summary>
public sealed class TrendDiscoveryAIResponse
{
    /// <summary>The full prompt that was sent to the AI provider (audit trail).</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Raw JSON response from the AI provider (contains the keyword array).</summary>
    public string RawJson { get; set; } = "[]";

    /// <summary>Provider display name, e.g. "DeepSeek".</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Model name, e.g. "deepseek-chat".</summary>
    public string Model { get; set; } = string.Empty;

    public int? TokensInput { get; set; }

    public int? TokensOutput { get; set; }

    /// <summary>AI call execution time in milliseconds.</summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>Whether the AI call completed successfully.</summary>
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }
}