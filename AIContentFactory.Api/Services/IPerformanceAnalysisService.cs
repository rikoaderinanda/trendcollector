using AIContentFactory.Api.Models.Analysis;

namespace AIContentFactory.Api.Services;

/// <summary>
/// Computes normalized performance metrics for candidate videos so the AI
/// provider receives summarized evidence rather than raw statistics.
/// </summary>
public interface IPerformanceAnalysisService
{
    /// <summary>
    /// Computes performance metrics for a video from its statistics history.
    /// Returns null when the video has no statistics at all.
    /// </summary>
    Task<VideoPerformanceSummary?> AnalyzeAsync(
        long videoId,
        CancellationToken cancellationToken = default);
}