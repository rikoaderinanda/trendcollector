namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Persisted raw AI request/response for a Viral Analysis run.
/// Used for prompt evaluation and model comparison in future versions.
/// </summary>
public sealed class ViralAnalysisPromptHistory
{
    public long Id { get; set; }

    /// <summary>FK to viral_analysis_runs.id.</summary>
    public long AnalysisRunId { get; set; }

    public string Prompt { get; set; } = string.Empty;

    /// <summary>Raw AI response - never discarded.</summary>
    public string AiResponse { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    public double? Temperature { get; set; }
    public int? TokensInput { get; set; }
    public int? TokensOutput { get; set; }
    public long ExecutionTimeMs { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}