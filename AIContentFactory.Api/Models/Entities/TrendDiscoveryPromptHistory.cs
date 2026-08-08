namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Full audit trail of a prompt sent to an AI provider and its raw response.
/// Never discarded - used to improve future prompts.
/// </summary>
public sealed class TrendDiscoveryPromptHistory
{
    public long Id { get; set; }

    /// <summary>Job that triggered this prompt.</summary>
    public long? JobId { get; set; }

    /// <summary>The full prompt sent to the AI provider.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Raw JSON response from the AI provider.</summary>
    public string? AiResponse { get; set; }

    /// <summary>Provider name, e.g. "DeepSeek", "OpenAI".</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Model name, e.g. "deepseek-chat".</summary>
    public string? Model { get; set; }

    public int? TokensInput { get; set; }

    public int? TokensOutput { get; set; }

    /// <summary>AI call execution time in milliseconds.</summary>
    public long? ExecutionTimeMs { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}