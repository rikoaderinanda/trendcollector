using Dapper;
using AIContentFactory.Api.Data;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <inheritdoc cref="IViralAnalysisRepository" />
public sealed class ViralAnalysisRepository : IViralAnalysisRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public ViralAnalysisRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // ---- Analysis Runs ----

    public async Task<long> InsertRunAsync(ViralAnalysisRun run, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO viral_analysis_runs (
                started_at, status, niche, trend_keyword, date_from, date_to,
                total_candidates, eligible_candidates, opportunities_generated,
                recommended_opportunity_id, trend_summary, market_observation,
                confidence_score, analysis_version, error_message
            )
            VALUES (
                @StartedAt, @Status, @Niche, @TrendKeyword, @DateFrom, @DateTo,
                @TotalCandidates, @EligibleCandidates, @OpportunitiesGenerated,
                @RecommendedOpportunityId, @TrendSummary, @MarketObservation,
                @ConfidenceScore, @AnalysisVersion, @ErrorMessage
            )
            RETURNING id;
            """;

        return await connection.ExecuteScalarAsync<long>(sql, run, commandTimeout: 30);
    }

    public async Task<ViralAnalysisRun?> GetRunByIdAsync(long runId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                id                        AS Id,
                started_at                AS StartedAt,
                finished_at               AS FinishedAt,
                status                    AS Status,
                niche                     AS Niche,
                trend_keyword             AS TrendKeyword,
                date_from                 AS DateFrom,
                date_to                   AS DateTo,
                total_candidates          AS TotalCandidates,
                eligible_candidates       AS EligibleCandidates,
                opportunities_generated   AS OpportunitiesGenerated,
                recommended_opportunity_id AS RecommendedOpportunityId,
                trend_summary             AS TrendSummary,
                market_observation        AS MarketObservation,
                confidence_score          AS ConfidenceScore,
                analysis_version          AS AnalysisVersion,
                error_message             AS ErrorMessage,
                created_at                AS CreatedAt
            FROM viral_analysis_runs
            WHERE id = @RunId;
            """;

        return await connection.QuerySingleOrDefaultAsync<ViralAnalysisRun>(sql,
            new { RunId = runId }, commandTimeout: 30);
    }

    public async Task UpdateRunAsync(ViralAnalysisRun run, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            UPDATE viral_analysis_runs
            SET finished_at              = @FinishedAt,
                status                   = @Status,
                total_candidates         = @TotalCandidates,
                eligible_candidates      = @EligibleCandidates,
                opportunities_generated  = @OpportunitiesGenerated,
                recommended_opportunity_id = @RecommendedOpportunityId,
                trend_summary            = @TrendSummary,
                market_observation       = @MarketObservation,
                confidence_score         = @ConfidenceScore,
                analysis_version         = @AnalysisVersion,
                error_message            = @ErrorMessage
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(sql, run, commandTimeout: 30);
    }

    public async Task<ViralAnalysisRun?> GetLatestCompletedRunAsync(
        string? niche,
        string? trendKeyword,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                id                        AS Id,
                started_at                AS StartedAt,
                finished_at               AS FinishedAt,
                status                    AS Status,
                niche                     AS Niche,
                trend_keyword             AS TrendKeyword,
                date_from                 AS DateFrom,
                date_to                   AS DateTo,
                total_candidates          AS TotalCandidates,
                eligible_candidates       AS EligibleCandidates,
                opportunities_generated   AS OpportunitiesGenerated,
                recommended_opportunity_id AS RecommendedOpportunityId,
                trend_summary             AS TrendSummary,
                market_observation        AS MarketObservation,
                confidence_score          AS ConfidenceScore,
                analysis_version          AS AnalysisVersion,
                error_message             AS ErrorMessage,
                created_at                AS CreatedAt
            FROM viral_analysis_runs
            WHERE status = 'Completed'
              AND (@Niche IS NULL OR niche = @Niche)
              AND (@TrendKeyword IS NULL OR trend_keyword = @TrendKeyword)
            ORDER BY started_at DESC
            LIMIT 1;
            """;

        return await connection.QuerySingleOrDefaultAsync<ViralAnalysisRun>(sql,
            new { Niche = niche, TrendKeyword = trendKeyword }, commandTimeout: 30);
    }

    // ---- Winning Patterns ----

    public async Task InsertPatternsAsync(IEnumerable<WinningPattern> patterns, CancellationToken cancellationToken = default)
    {
        var patternList = patterns.ToList();
        if (patternList.Count == 0)
        {
            return;
        }

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO viral_analysis_winning_patterns (
                analysis_run_id, pattern_type, pattern_name, description,
                frequency, supporting_video_count, average_momentum_score, evidence
            )
            VALUES (
                @AnalysisRunId, @PatternType, @PatternName, @Description,
                @Frequency, @SupportingVideoCount, @AverageMomentumScore, @Evidence
            );
            """;

        await connection.ExecuteAsync(sql, patternList, commandTimeout: 30);
    }

    public async Task<IEnumerable<WinningPattern>> GetPatternsByRunIdAsync(long runId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                id                      AS Id,
                analysis_run_id         AS AnalysisRunId,
                pattern_type            AS PatternType,
                pattern_name            AS PatternName,
                description             AS Description,
                frequency               AS Frequency,
                supporting_video_count  AS SupportingVideoCount,
                average_momentum_score  AS AverageMomentumScore,
                evidence                AS Evidence,
                created_at              AS CreatedAt
            FROM viral_analysis_winning_patterns
            WHERE analysis_run_id = @RunId
            ORDER BY supporting_video_count DESC, average_momentum_score DESC;
            """;

        return await connection.QueryAsync<WinningPattern>(sql,
            new { RunId = runId }, commandTimeout: 30);
    }

    // ---- Content Opportunities ----

    public async Task<long> InsertOpportunityAsync(ContentOpportunity opportunity, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO viral_analysis_content_opportunities (
                analysis_run_id, rank, topic, angle, target_audience,
                hook, format, structure, emotion, psychological_trigger,
                why_now, content_gap, differentiation_strategy,
                opportunity_score, confidence_score, risk_level,
                supporting_video_ids, evidence
            )
            VALUES (
                @AnalysisRunId, @Rank, @Topic, @Angle, @TargetAudience,
                @Hook, @Format, @Structure, @Emotion, @PsychologicalTrigger,
                @WhyNow, @ContentGap, @DifferentiationStrategy,
                @OpportunityScore, @ConfidenceScore, @RiskLevel,
                @SupportingVideoIds, @Evidence
            )
            RETURNING id;
            """;

        return await connection.ExecuteScalarAsync<long>(sql, opportunity, commandTimeout: 30);
    }

    public async Task<IEnumerable<ContentOpportunity>> GetOpportunitiesByRunIdAsync(long runId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                id                       AS Id,
                analysis_run_id          AS AnalysisRunId,
                rank                     AS Rank,
                topic                    AS Topic,
                angle                    AS Angle,
                target_audience          AS TargetAudience,
                hook                     AS Hook,
                format                   AS Format,
                structure                AS Structure,
                emotion                  AS Emotion,
                psychological_trigger    AS PsychologicalTrigger,
                why_now                  AS WhyNow,
                content_gap              AS ContentGap,
                differentiation_strategy AS DifferentiationStrategy,
                opportunity_score        AS OpportunityScore,
                confidence_score         AS ConfidenceScore,
                risk_level               AS RiskLevel,
                supporting_video_ids     AS SupportingVideoIds,
                evidence                 AS Evidence,
                created_at               AS CreatedAt
            FROM viral_analysis_content_opportunities
            WHERE analysis_run_id = @RunId
            ORDER BY rank ASC;
            """;

        return await connection.QueryAsync<ContentOpportunity>(sql,
            new { RunId = runId }, commandTimeout: 30);
    }

    public async Task<ContentOpportunity?> GetRecommendedOpportunityAsync(long runId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                id                       AS Id,
                analysis_run_id          AS AnalysisRunId,
                rank                     AS Rank,
                topic                    AS Topic,
                angle                    AS Angle,
                target_audience          AS TargetAudience,
                hook                     AS Hook,
                format                   AS Format,
                structure                AS Structure,
                emotion                  AS Emotion,
                psychological_trigger    AS PsychologicalTrigger,
                why_now                  AS WhyNow,
                content_gap              AS ContentGap,
                differentiation_strategy AS DifferentiationStrategy,
                opportunity_score        AS OpportunityScore,
                confidence_score         AS ConfidenceScore,
                risk_level               AS RiskLevel,
                supporting_video_ids     AS SupportingVideoIds,
                evidence                 AS Evidence,
                created_at               AS CreatedAt
            FROM viral_analysis_content_opportunities
            WHERE analysis_run_id = @RunId
            ORDER BY rank ASC
            LIMIT 1;
            """;

        return await connection.QuerySingleOrDefaultAsync<ContentOpportunity>(sql,
            new { RunId = runId }, commandTimeout: 30);
    }

    // ---- Prompt History ----

    public async Task InsertPromptHistoryAsync(ViralAnalysisPromptHistory history, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO viral_analysis_prompt_history (
                analysis_run_id, prompt, ai_response, provider, model,
                temperature, tokens_input, tokens_output, execution_time_ms
            )
            VALUES (
                @AnalysisRunId, @Prompt, @AiResponse, @Provider, @Model,
                @Temperature, @TokensInput, @TokensOutput, @ExecutionTimeMs
            );
            """;

        await connection.ExecuteAsync(sql, history, commandTimeout: 30);
    }

    // ---- Candidate Snapshots ----

    public async Task InsertCandidatesAsync(IEnumerable<ViralAnalysisCandidateSnapshot> candidates, CancellationToken cancellationToken = default)
    {
        var candidateList = candidates.ToList();
        if (candidateList.Count == 0)
        {
            return;
        }

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO viral_analysis_candidate_snapshots (
                analysis_run_id, video_id, is_eligible, skip_reason,
                performance_summary_json, pattern_summary_json
            )
            VALUES (
                @AnalysisRunId, @VideoId, @IsEligible, @SkipReason,
                @PerformanceSummaryJson::jsonb, @PatternSummaryJson::jsonb
            );
            """;

        await connection.ExecuteAsync(sql, candidateList, commandTimeout: 30);
    }
}