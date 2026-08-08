namespace AIContentFactory.Api.AI;

/// <summary>
/// Unified response from any AI knowledge extraction provider.
/// Carries the raw JSON plus telemetry for the audit trail.
/// </summary>
public sealed class KnowledgeExtractionResponse
{
    public bool Success { get; set; }

    /// <summary>Raw JSON returned by the provider.</summary>
    public string? RawJson { get; set; }

    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Prompt { get; set; }

    public long ExecutionTimeMs { get; set; }
    public int? TokensInput { get; set; }
    public int? TokensOutput { get; set; }

    public string? ErrorMessage { get; set; }
}