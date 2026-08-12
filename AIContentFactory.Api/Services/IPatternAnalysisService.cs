using AIContentFactory.Api.Models.Analysis;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Services;

/// <summary>
/// Analyzes content patterns from Agent 2 knowledge and detects recurring
/// winning patterns across multiple high-performing videos.
/// </summary>
public interface IPatternAnalysisService
{
    /// <summary>
    /// Detects winning patterns by comparing eligible candidates against each other.
    /// Only the top candidates by momentum score are used for cross-video detection.
    /// </summary>
    /// <param name="eligibleCandidates">Eligible candidates with performance data.</param>
    /// <param name="analysisRunId">The run these patterns belong to.</param>
    /// <param name="topN">How many top candidates to compare.</param>
    IReadOnlyList<WinningPattern> DetectWinningPatterns(
        IReadOnlyList<AnalysisCandidate> eligibleCandidates,
        long analysisRunId,
        int topN = 5);
}