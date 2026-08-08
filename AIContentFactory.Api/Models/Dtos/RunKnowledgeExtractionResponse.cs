namespace AIContentFactory.Api.Models.Dtos;

/// <summary>
/// Result of a manual knowledge extraction run or retry.
/// </summary>
public sealed class RunKnowledgeExtractionResponse
{
    public long QueueId { get; set; }
    public long VideoId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
}