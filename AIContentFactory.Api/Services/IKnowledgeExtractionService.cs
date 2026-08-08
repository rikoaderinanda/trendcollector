namespace AIContentFactory.Api.Services;

/// <summary>
/// Orchestrates the knowledge extraction pipeline for a single video.
/// </summary>
public interface IKnowledgeExtractionService
{
    /// <summary>
    /// Runs the full pipeline for a queue item:
    /// load video metadata, retrieve transcript, generate prompt,
    /// call AI, persist knowledge.
    /// </summary>
    Task ProcessQueueItemAsync(
        long queueId,
        CancellationToken cancellationToken = default);
}