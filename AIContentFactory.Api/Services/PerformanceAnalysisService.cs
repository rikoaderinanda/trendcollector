using AIContentFactory.Api.Models.Analysis;
using AIContentFactory.Api.Repositories;

namespace AIContentFactory.Api.Services;

/// <inheritdoc cref="IPerformanceAnalysisService" />
public sealed class PerformanceAnalysisService : IPerformanceAnalysisService
{
    private readonly IVideoRepository _videoRepository;
    private readonly StatisticsCalculator _statisticsCalculator;

    public PerformanceAnalysisService(
        IVideoRepository videoRepository,
        StatisticsCalculator statisticsCalculator)
    {
        _videoRepository = videoRepository;
        _statisticsCalculator = statisticsCalculator;
    }

    public async Task<VideoPerformanceSummary?> AnalyzeAsync(
        long videoId,
        CancellationToken cancellationToken = default)
    {
        var history = (await _videoRepository.GetStatisticsHistoryAsync(videoId, cancellationToken)).ToList();
        if (history.Count == 0)
        {
            return null;
        }

        var latest = history[^1];
        var previous = history.Count >= 2 ? history[^2] : null;

        // Reuse the existing velocity calculator from Agent 1.
        var velocity = _statisticsCalculator.CalculateVelocity(latest, previous);

        // Momentum score: prefer the persisted growth_score, otherwise fall back
        // to the freshly computed velocity.
        var momentumScore = latest.GrowthScore ?? velocity.GrowthScore ?? 0m;

        // Candidate score: weighted blend of momentum (0.7) and engagement (0.3).
        // A newer video with strong growth outranks an old video with many views.
        var engagementScore = ComputeEngagementScore(latest);
        var candidateScore = Math.Clamp(
            momentumScore * 0.7m + engagementScore * 0.3m,
            0m, 100m);

        return new VideoPerformanceSummary
        {
            VideoId = videoId,
            Views = latest.Views,
            Likes = latest.Likes,
            Comments = latest.Comments,
            VideoAgeDays = latest.VideoAgeDays,
            ViewsPerHour = latest.ViewsPerHour ?? velocity.ViewsPerHour,
            LikesPerHour = latest.LikeVelocity ?? velocity.LikeVelocity,
            CommentsPerHour = latest.CommentVelocity ?? velocity.CommentVelocity,
            EngagementRate = latest.EngagementRate,
            MomentumScore = Math.Round(momentumScore, 2),
            CandidateScore = Math.Round(candidateScore, 2),
            StatisticsSnapshotCount = history.Count
        };
    }

    /// <summary>
    /// Maps engagement metrics into a 0-100 score.
    /// Uses like ratio and comment ratio (fractions) with saturation points.
    /// </summary>
    private static decimal ComputeEngagementScore(AIContentFactory.Api.Models.Entities.VideoStatistics stats)
    {
        // Like ratio of 10% => full score (saturation).
        var likeRatio = stats.LikeRatio ?? 0m;
        var likeScore = Math.Min(likeRatio * 100m / 10m, 1m);

        // Comment ratio of 2% => full score (saturation).
        var commentRatio = stats.CommentRatio ?? 0m;
        var commentScore = Math.Min(commentRatio * 100m / 2m, 1m);

        // Views per hour of 50K => full score (saturation) - reuses the same
        // saturation constant as StatisticsCalculator.GrowthScore.
        var viewsPerHour = stats.ViewsPerHour ?? 0m;
        var velocityScore = Math.Min(viewsPerHour / 50_000m, 1m);

        var combined = (likeScore * 0.4m) + (commentScore * 0.3m) + (velocityScore * 0.3m);
        return Math.Round(Math.Clamp(combined * 100m, 0m, 100m), 2);
    }
}