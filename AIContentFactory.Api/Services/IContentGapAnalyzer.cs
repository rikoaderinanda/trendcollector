using AIContentFactory.Api.Models.Analysis;

namespace AIContentFactory.Api.Services;

/// <summary>
/// Detects content gaps - missing angles, underserved audiences, unanswered
/// questions and simplification opportunities - by comparing what existing
/// high-performing videos cover vs what they do not.
/// </summary>
public interface IContentGapAnalyzer
{
    /// <summary>
    /// Produces a formatted analysis of content gaps found among the eligible candidates.
    /// </summary>
    string AnalyzeGaps(IReadOnlyList<AnalysisCandidate> eligibleCandidates);
}