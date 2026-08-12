using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <summary>
/// Data access for Agent 3 (Viral Analyzer) persistence.
/// </summary>
public interface IViralAnalysisRepository
{
    // ---- Analysis Runs ----

    /// <summary>Creates a new analysis run and returns its id.</summary>
    Task<long> InsertRunAsync(ViralAnalysisRun run, CancellationToken cancellationToken = default);

    /// <summary>Gets a run by id, or null when not found.</summary>
    Task<ViralAnalysisRun?> GetRunByIdAsync(long runId, CancellationToken cancellationToken = default);

    /// <summary>Updates a run (status, counts, summary, recommendation FK, error).</summary>
    Task UpdateRunAsync(ViralAnalysisRun run, CancellationToken cancellationToken = default);

    /// <summary>Finds the most recent completed run, optionally filtered by niche/keyword.</summary>
    Task<ViralAnalysisRun?> GetLatestCompletedRunAsync(
        string? niche,
        string? trendKeyword,
        CancellationToken cancellationToken = default);

    /// <summary>Lists analysis runs, newest first, with pagination.</summary>
    Task<IEnumerable<ViralAnalysisRun>> GetRunsAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a completed analysis already exists for the given
    /// niche/keyword/date within the lookback window. Returns the existing
    /// run ID if found, otherwise null.
    /// </summary>
    Task<long?> FindExistingCompletedRunAsync(
        string? niche,
        string? trendKeyword,
        DateTime? dateFrom,
        DateTime? dateTo,
        int lookbackDays,
        CancellationToken cancellationToken = default);

    // ---- Winning Patterns ----

    /// <summary>Inserts a batch of winning patterns.</summary>
    Task InsertPatternsAsync(IEnumerable<WinningPattern> patterns, CancellationToken cancellationToken = default);

    /// <summary>Lists winning patterns for a run.</summary>
    Task<IEnumerable<WinningPattern>>
        GetPatternsByRunIdAsync(long runId, CancellationToken cancellationToken = default);

    // ---- Content Opportunities ----

    /// <summary>Inserts a content opportunity and returns its id.</summary>
    Task<long> InsertOpportunityAsync(ContentOpportunity opportunity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts all opportunities AND updates the run's recommended_opportunity_id
    /// in one transaction so the FK reference is always atomic.
    /// </summary>
    Task CompleteRunAsync(
        long runId,
        IEnumerable<ContentOpportunity> opportunities,
        long? recommendedOpportunityId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists ranked opportunities for a run, ascending by rank.</summary>
    Task<IEnumerable<ContentOpportunity>> GetOpportunitiesByRunIdAsync(long runId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the TOP 1 opportunity for a run, or null when none.</summary>
    Task<ContentOpportunity?> GetRecommendedOpportunityAsync(long runId, CancellationToken cancellationToken = default);

    // ---- Prompt History ----

    /// <summary>Persists the raw AI prompt/response so it is never discarded.</summary>
    Task InsertPromptHistoryAsync(ViralAnalysisPromptHistory history, CancellationToken cancellationToken = default);

    // ---- Candidate Snapshots ----

    /// <summary>Inserts candidate snapshots (eligibility, skip reason, metrics).</summary>
    Task InsertCandidatesAsync(IEnumerable<ViralAnalysisCandidateSnapshot> candidates,
        CancellationToken cancellationToken = default);
}