using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Models.Dtos;

/// <summary>
/// Full detail of a video in the knowledge extraction pipeline:
/// metadata, transcript, structured knowledge, queue status and execution time.
/// </summary>
public sealed class KnowledgeExtractionDetailDto
{
    public TrendingVideoMetadata? Metadata { get; set; }
    public VideoTranscript? Transcript { get; set; }
    public VideoKnowledge? Knowledge { get; set; }
    public KnowledgeExtractionQueue? Queue { get; set; }

    /// <summary>Total execution time of the latest run in milliseconds.</summary>
    public long? ExecutionTimeMs => Queue?.DurationMs;
}