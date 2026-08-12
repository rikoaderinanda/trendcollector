namespace AIContentFactory.Api.AI;

/// <summary>
/// Result of AI transcript polishing: the cleaned/polished text plus an
/// AI-assessed quality score (0-100).
/// </summary>
public sealed class TranscriptPolishResponse
{
    public bool Success { get; set; }

    /// <summary>Polished transcript text (grammar fixed, filler removed, paragraphs added).</summary>
    public string? PolishedText { get; set; }

    /// <summary>AI-assessed quality score 0-100.</summary>
    public int Score { get; set; }

    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    public long ExecutionTimeMs { get; set; }
    public int? TokensInput { get; set; }
    public int? TokensOutput { get; set; }

    public string? ErrorMessage { get; set; }
}