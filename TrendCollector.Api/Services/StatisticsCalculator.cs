using TrendCollector.Api.Models.Entities;

namespace TrendCollector.Api.Services;

/// <summary>
/// Calculates derived engagement metrics for a video statistics snapshot.
/// </summary>
public sealed class StatisticsCalculator
{
    /// <summary>
    /// Builds a <see cref="VideoStatistics"/> snapshot and computes the derived metrics.
    /// A views value of zero yields zero ratios. Missing likes/comments are treated as zero for calculations.
    /// </summary>
    public VideoStatistics Calculate(
        long videoId,
        long? views,
        long? likes,
        long? comments,
        long? favorites,
        DateTimeOffset? publishedAt,
        DateTimeOffset capturedAt)
    {
        var ageDays = CalculateAgeDays(publishedAt, capturedAt);
        var safeViews = views ?? 0;

        decimal? engagementRate = null;
        decimal? likeRatio = null;
        decimal? commentRatio = null;
        decimal? viewPerDay = null;

        if (safeViews > 0)
        {
            var safeLikes = likes ?? 0;
            var safeComments = comments ?? 0;

            engagementRate = Math.Round((safeLikes + safeComments) / (decimal)safeViews * 100m, 4);
            likeRatio = Math.Round(safeLikes / (decimal)safeViews * 100m, 4);
            commentRatio = Math.Round(safeComments / (decimal)safeViews * 100m, 4);
            viewPerDay = Math.Round(safeViews / (decimal)ageDays, 4);
        }

        return new VideoStatistics
        {
            VideoId = videoId,
            Views = views,
            Likes = likes,
            Comments = comments,
            Favorites = favorites,
            EngagementRate = engagementRate,
            LikeRatio = likeRatio,
            CommentRatio = commentRatio,
            ViewPerDay = viewPerDay,
            VideoAgeDays = ageDays,
            CapturedAt = capturedAt
        };
    }

    /// <summary>Days between published and captured, minimum 1.</summary>
    private static int CalculateAgeDays(DateTimeOffset? publishedAt, DateTimeOffset capturedAt)
    {
        if (publishedAt is null)
        {
            return 1;
        }

        var age = (capturedAt - publishedAt.Value).TotalDays;
        if (age <= 0)
        {
            return 1;
        }

        // Ceiling so a 1.2-day-old video counts as 2 days, never 0 or below.
        return (int)Math.Ceiling(age);
    }
}