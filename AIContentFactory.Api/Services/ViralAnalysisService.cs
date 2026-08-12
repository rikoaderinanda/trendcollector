using System.Text;
using System.Text.Json;
using AIContentFactory.Api.AI;
using AIContentFactory.Api.Configuration;
using AIContentFactory.Api.Models.Analysis;
using AIContentFactory.Api.Models.Dtos;
using AIContentFactory.Api.Models.Entities;
using AIContentFactory.Api.Repositories;
using Microsoft.Extensions.Options;

namespace AIContentFactory.Api.Services;

/// <inheritdoc cref="IViralAnalysisService" />
public sealed class ViralAnalysisService : IViralAnalysisService
{
    private readonly IViralAnalysisRepository _analysisRepository;
    private readonly IVideoRepository _videoRepository;
    private readonly IVideoKnowledgeRepository _knowledgeRepository;
    private readonly IVideoTranscriptRepository _transcriptRepository;
    private readonly IPerformanceAnalysisService _performanceAnalysis;
    private readonly IPatternAnalysisService _patternAnalysis;
    private readonly IContentGapAnalyzer _gapAnalyzer;
    private readonly IContentOpportunityScorer _opportunityScorer;
    private readonly ITrendClassifier _trendClassifier;
    private readonly RetryCalculator _retryCalculator;
    private readonly IViralAnalysisProvider _aiProvider;
    private readonly ViralAnalysisOptions _options;
    private readonly ILogger<ViralAnalysisService> _logger;

    public ViralAnalysisService(
        IViralAnalysisRepository analysisRepository,
        IVideoRepository videoRepository,
        IVideoKnowledgeRepository knowledgeRepository,
        IVideoTranscriptRepository transcriptRepository,
        IPerformanceAnalysisService performanceAnalysis,
        IPatternAnalysisService patternAnalysis,
        IContentGapAnalyzer gapAnalyzer,
        IContentOpportunityScorer opportunityScorer,
        ITrendClassifier trendClassifier,
        RetryCalculator retryCalculator,
        IViralAnalysisProvider aiProvider,
        IOptions<ViralAnalysisOptions> options,
        ILogger<ViralAnalysisService> logger)
    {
        _analysisRepository = analysisRepository;
        _videoRepository = videoRepository;
        _knowledgeRepository = knowledgeRepository;
        _transcriptRepository = transcriptRepository;
        _performanceAnalysis = performanceAnalysis;
        _patternAnalysis = patternAnalysis;
        _gapAnalyzer = gapAnalyzer;
        _opportunityScorer = opportunityScorer;
        _trendClassifier = trendClassifier;
        _retryCalculator = retryCalculator;
        _aiProvider = aiProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<long> RunAsync(RunViralAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("Viral Analysis started at {StartedAt:O}.", startedAt);

        // 1. Create the analysis run.
        var run = new ViralAnalysisRun
        {
            StartedAt = startedAt,
            Status = "Running",
            Niche = request.Niche,
            TrendKeyword = request.TrendKeyword,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            AnalysisVersion = _options.PromptVersion
        };

        var runId = await _analysisRepository.InsertRunAsync(run, cancellationToken);
        run.Id = runId;

        // Idempotency: check if a completed analysis already exists for this
        // niche/keyword/date within the lookback window.
        var existingRunId = await _analysisRepository.FindExistingCompletedRunAsync(
            request.Niche, request.TrendKeyword,
            request.DateFrom, request.DateTo,
            _options.LookbackDays,
            cancellationToken);

        if (existingRunId.HasValue)
        {
            _logger.LogInformation(
                "Duplicate analysis detected for Niche='{Niche}', Keyword='{Keyword}'. " +
                "Returning existing completed run {ExistingId}.",
                request.Niche, request.TrendKeyword, existingRunId.Value);

            // Mark the just-created run as Duplicate and return the existing one.
            run.Status = "Duplicate";
            run.FinishedAt = DateTimeOffset.UtcNow;
            run.ErrorMessage = $"Duplicate of existing completed run {existingRunId.Value}.";
            await _analysisRepository.UpdateRunAsync(run, cancellationToken);

            return existingRunId.Value;
        }

        try
        {
            // 2. Load candidate videos.
            var videosLimit = Math.Max(1, Math.Min(request.MaximumVideos, _options.MaxVideosPerAnalysis));
            var recentVideos = await _videoRepository.ListRecentAsync(_options.LookbackDays, cancellationToken);
            var candidates = new List<AnalysisCandidate>();

            _logger.LogInformation("Candidate video count: {Count}.", recentVideos.Count());

            foreach (var video in recentVideos.Take(videosLimit))
            {
                var candidate = await BuildCandidateAsync(video, request, cancellationToken);
                candidates.Add(candidate);
            }

            var eligible = candidates.Where(c => c.IsEligible).ToList();
            var totalCandidates = candidates.Count;
            var eligibleCount = eligible.Count;

            _logger.LogInformation("Eligible candidate count: {Count} of {Total}.", eligibleCount, totalCandidates);

            // 3. Persist candidate snapshots (auditability).
            await _analysisRepository.InsertCandidatesAsync(
                candidates.Select(c => new ViralAnalysisCandidateSnapshot
                {
                    AnalysisRunId = runId,
                    VideoId = c.VideoId,
                    IsEligible = c.IsEligible,
                    SkipReason = c.SkipReason,
                    PerformanceSummaryJson = c.Performance is null ? null : JsonSerializer.Serialize(c.Performance),
                    PatternSummaryJson = c.Knowledge is null ? null : JsonSerializer.Serialize(c.Knowledge)
                }),
                cancellationToken);

            // 4. Cross-video pattern detection.
            var patterns = _patternAnalysis.DetectWinningPatterns(eligible, runId);
            await _analysisRepository.InsertPatternsAsync(patterns, cancellationToken);

            // 5. Content gap analysis.
            var gaps = _gapAnalyzer.AnalyzeGaps(eligible);

            // 6. Trend summary (from candidate data; classified + refined by AI in step 7).
            var trendClassification = _trendClassifier.Classify(eligible);
            var trendSummary = BuildTrendSummary(eligible, gaps, trendClassification);

            // 7. Call the AI provider (if eligible candidates exist).
            // Confidence starts null - it is only populated from AI evidence.
            decimal? confidenceScore = null;
            var opportunities = new List<ContentOpportunity>();

            if (eligibleCount > 0)
            {
                var aiRequest = new ViralAnalysisRequest
                {
                    AnalysisRunId = runId,
                    Niche = request.Niche,
                    TrendKeyword = request.TrendKeyword,
                    CandidateSummaries = BuildCandidateSummaries(eligible),
                    WinningPatterns = BuildPatternsText(patterns),
                    TrendSummary = trendSummary,
                    ContentGaps = gaps,
                    OpportunityCount = _options.OpportunityCount
                };

                _logger.LogInformation("Calling AI provider ({Provider} / {Model}).", _aiProvider.ProviderName,
                    _aiProvider.ModelName);
                var aiResponse = await CallAiWithRetryAsync(aiRequest, runId, cancellationToken);

                // Persist raw AI response - never discard.
                await _analysisRepository.InsertPromptHistoryAsync(new ViralAnalysisPromptHistory
                {
                    AnalysisRunId = runId,
                    Prompt = aiResponse.Prompt ?? string.Empty,
                    AiResponse = aiResponse.RawJson ?? string.Empty,
                    Provider = aiResponse.Provider,
                    Model = aiResponse.Model,
                    Temperature = _options.Temperature,
                    TokensInput = aiResponse.TokensInput,
                    TokensOutput = aiResponse.TokensOutput,
                    ExecutionTimeMs = aiResponse.ExecutionTimeMs
                }, cancellationToken);

                if (aiResponse.Success && !string.IsNullOrWhiteSpace(aiResponse.RawJson))
                {
                    var parsed = ParseAiResponse(aiResponse.RawJson);
                    trendSummary = parsed.TrendSummary ?? trendSummary;
                    confidenceScore = parsed.ConfidenceScore;
                    opportunities = BuildOpportunities(parsed, runId, eligible);
                }
                else
                {
                    _logger.LogWarning("AI provider failed for run {RunId}: {Error}", runId, aiResponse.ErrorMessage);
                }
            }

            // 8. Rank opportunities and pick TOP 1.
            var ranked = opportunities
                .OrderByDescending(o => o.OpportunityScore)
                .ToList();

            for (var i = 0; i < ranked.Count; i++)
            {
                ranked[i].Rank = i + 1;
            }

            var recommended = ranked.FirstOrDefault();
            long? recommendedId = null;

            // Insert opportunities + set recommended FK atomically (single transaction).
            if (ranked.Count > 0)
            {
                await _analysisRepository.CompleteRunAsync(runId, ranked, null, cancellationToken);

                // Re-fetch the run to read the recommended_opportunity_id set
                // by the transaction (the rank-1 row).
                var completedRun = await _analysisRepository.GetRunByIdAsync(runId, cancellationToken);
                recommendedId = completedRun?.RecommendedOpportunityId;
            }

            // 9. Update the run.
            run.FinishedAt = DateTimeOffset.UtcNow;
            run.Status = "Completed";
            run.TotalCandidates = totalCandidates;
            run.EligibleCandidates = eligibleCount;
            run.OpportunitiesGenerated = ranked.Count;
            run.RecommendedOpportunityId = recommendedId;
            run.TrendSummary = trendSummary;
            run.MarketObservation = BuildMarketObservation(opportunities);
            run.ConfidenceScore = confidenceScore;

            await _analysisRepository.UpdateRunAsync(run, cancellationToken);

            _logger.LogInformation(
                "Viral Analysis completed. Run {RunId}: {Eligible}/{Total} candidates, {Opportunities} opportunities, TOP 1 = '{Topic}'.",
                runId, eligibleCount, totalCandidates, ranked.Count, recommended?.Topic);

            return runId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Viral Analysis run {RunId} failed.", runId);

            run.Status = "Failed";
            run.FinishedAt = DateTimeOffset.UtcNow;
            run.ErrorMessage = ex.Message;
            await _analysisRepository.UpdateRunAsync(run, cancellationToken);

            throw;
        }
    }

    // ---------- Candidate assembly ----------

    private async Task<AnalysisCandidate> BuildCandidateAsync(
        TrendingVideo video,
        RunViralAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        var candidate = new AnalysisCandidate
        {
            VideoId = video.Id,
            Title = video.Title ?? string.Empty,
            Description = video.Description,
            Tags = video.Tags,
            Language = video.Language,
            PublishedAt = video.PublishedAt
        };

        // Filter by niche/keyword when specified.
        if (!string.IsNullOrWhiteSpace(request.Niche)
            && !MatchesNiche(video, request.Niche))
        {
            candidate.IsEligible = false;
            candidate.SkipReason = $"Niche '{request.Niche}' does not match video metadata.";
            return candidate;
        }

        if (!string.IsNullOrWhiteSpace(request.TrendKeyword)
            && !MatchesKeyword(video, request.TrendKeyword))
        {
            candidate.IsEligible = false;
            candidate.SkipReason = $"Trend keyword '{request.TrendKeyword}' does not match video metadata.";
            return candidate;
        }

        // Load statistics.
        var stats = await _videoRepository.GetLatestStatisticsAsync(video.Id, cancellationToken);
        if (stats is null)
        {
            candidate.IsEligible = false;
            candidate.SkipReason = "Missing statistics snapshot.";
            return candidate;
        }

        candidate.Statistics = stats;

        // Compute performance metrics.
        var performance = await _performanceAnalysis.AnalyzeAsync(video.Id, cancellationToken);
        if (performance is null)
        {
            candidate.IsEligible = false;
            candidate.SkipReason = "No statistics history available for performance analysis.";
            return candidate;
        }

        candidate.Performance = performance;

        // Minimum candidate score filter.
        if (performance.CandidateScore < request.MinimumCandidateScore)
        {
            candidate.IsEligible = false;
            candidate.SkipReason =
                $"Candidate score {performance.CandidateScore} below minimum {request.MinimumCandidateScore}.";
            return candidate;
        }

        // Load knowledge.
        var knowledge = await _knowledgeRepository.GetByVideoIdAsync(video.Id, cancellationToken);
        if (knowledge is null)
        {
            candidate.IsEligible = false;
            candidate.SkipReason = "Missing video knowledge (Agent 2 has not completed).";
            return candidate;
        }

        candidate.Knowledge = knowledge;

        // Load transcript (excerpt only).
        var transcript = await _transcriptRepository.GetByVideoIdAsync(video.Id, cancellationToken);
        if (transcript is not null)
        {
            candidate.Transcript = Truncate(transcript.Transcript, _options.MaxTranscriptCharacters);
        }

        // Eligibility: knowledge must exist; transcript may be missing if
        // knowledge already contains a valid summary.
        if (candidate.Knowledge is null
            || (candidate.Transcript is null && string.IsNullOrWhiteSpace(candidate.Knowledge.Summary)))
        {
            candidate.IsEligible = false;
            candidate.SkipReason = "No transcript and no valid knowledge summary available.";
            return candidate;
        }

        candidate.IsEligible = true;
        return candidate;
    }

    // ---------- Text formatting ----------

    private string BuildCandidateSummaries(IReadOnlyList<AnalysisCandidate> candidates)
    {
        var sb = new StringBuilder();
        foreach (var candidate in candidates.OrderByDescending(c => c.Performance?.MomentumScore))
        {
            var p = candidate.Performance;
            var k = candidate.Knowledge;

            sb.AppendLine($"VideoId: {candidate.VideoId}");
            sb.AppendLine($"  Title: {candidate.Title}");
            sb.AppendLine($"  Topic: {k?.MainTopic ?? "(none)"}");
            sb.AppendLine(
                $"  Views: {FormatNumber(p?.Views)} | Likes: {FormatNumber(p?.Likes)} | Comments: {FormatNumber(p?.Comments)}");
            sb.AppendLine(
                $"  Views/hour: {FormatNumber(p?.ViewsPerHour)} | Likes/hour: {FormatNumber(p?.LikesPerHour)} | Comments/hour: {FormatNumber(p?.CommentsPerHour)}");
            sb.AppendLine($"  Engagement rate: {FormatPercent(p?.EngagementRate)} | Age: {p?.VideoAgeDays ?? 0} days");
            sb.AppendLine(
                $"  Momentum: {p?.MomentumScore:0.0}/100 | Candidate: {p?.CandidateScore:0.0}/100 | Snapshots: {p?.StatisticsSnapshotCount}");
            sb.AppendLine($"  Hook: {k?.Hook ?? "(none)"}");
            sb.AppendLine($"  Structure: {Join(k?.ContentStructure)}");
            sb.AppendLine($"  Emotion: {k?.Emotion ?? "(none)"} | Tone: {k?.Tone ?? "(none)"}");
            sb.AppendLine($"  Triggers: {Join(k?.PsychologicalTriggers)}");
            sb.AppendLine($"  Engagement techniques: {Join(k?.EngagementTechniques)}");
            sb.AppendLine($"  Audience: {k?.TargetAudience ?? "(none)"} | Content type: {k?.ContentType ?? "(none)"}");
            sb.AppendLine($"  Summary: {Truncate(k?.Summary ?? "(none)", 300)}");
            if (!string.IsNullOrWhiteSpace(candidate.Transcript))
            {
                sb.AppendLine($"  Transcript excerpt: {Truncate(candidate.Transcript, 500)}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildPatternsText(IReadOnlyList<WinningPattern> patterns)
    {
        if (patterns.Count == 0)
        {
            return "No recurring patterns detected.";
        }

        var sb = new StringBuilder();
        foreach (var pattern in patterns)
        {
            sb.AppendLine(
                $"- {pattern.PatternType}: {pattern.PatternName} ({pattern.SupportingVideoCount}/{pattern.Frequency} videos, avg momentum {pattern.AverageMomentumScore:0.0})");
            sb.AppendLine($"  {pattern.Evidence}");
        }

        return sb.ToString();
    }

    private string BuildTrendSummary(IReadOnlyList<AnalysisCandidate> eligible, string gaps,
        TrendClassification classification)
    {
        if (eligible.Count == 0)
        {
            return "No eligible candidates. Analysis could not identify a trend.";
        }

        var top = eligible.OrderByDescending(c => c.Performance?.MomentumScore ?? 0m).Take(3).ToList();
        var topics = top.Where(c => c.Knowledge?.MainTopic is not null)
            .Select(c => c.Knowledge!.MainTopic!)
            .Distinct()
            .ToList();

        var avgMomentum = eligible.Average(c => c.Performance?.MomentumScore ?? 0m);
        var sb = new StringBuilder();
        sb.AppendLine($"Trend Classification: {classification.Label} — {classification.Explanation}");
        sb.AppendLine($"Analyzed {eligible.Count} eligible candidates. Average momentum: {avgMomentum:0.0}/100.");
        if (topics.Count > 0)
        {
            sb.AppendLine("Top topics: " + string.Join(", ", topics.Take(3)));
        }

        if (!string.IsNullOrWhiteSpace(gaps))
        {
            sb.AppendLine("Gap signals: " + Truncate(gaps, 500));
        }

        return sb.ToString();
    }

    private static string BuildMarketObservation(IReadOnlyList<ContentOpportunity> opportunities)
    {
        if (opportunities.Count == 0)
        {
            return "No opportunities could be generated.";
        }

        var top = opportunities[0];
        return $"The market is favoring {top.Topic} with {top.SupportingVideoIds?.Length ?? 0} supporting video(s) " +
               $"using {top.PsychologicalTrigger ?? "strong psychological triggers"} and " +
               $"{top.Emotion ?? "high emotional engagement"}.";
    }

    // ---------- AI response parsing ----------

    private static AiAnalysisResult ParseAiResponse(string rawJson)
    {
        var result = new AiAnalysisResult();
        var clean = ExtractJsonObject(rawJson);
        if (string.IsNullOrWhiteSpace(clean))
        {
            return result;
        }

        using var doc = JsonDocument.Parse(clean);
        var root = doc.RootElement;

        result.TrendSummary = TryGetString(root, "trendSummary");
        result.MarketObservation = TryGetString(root, "marketObservation");
        result.ConfidenceScore = TryGetDecimal(root, "confidenceScore");

        if (root.TryGetProperty("opportunities", out var opportunities))
        {
            foreach (var opp in opportunities.EnumerateArray())
            {
                var draft = new ContentOpportunityDraft
                {
                    Topic = TryGetString(opp, "topic") ?? string.Empty,
                    Angle = TryGetString(opp, "angle"),
                    TargetAudience = TryGetString(opp, "targetAudience"),
                    Hook = TryGetString(opp, "hook"),
                    Format = TryGetString(opp, "format"),
                    Structure = TryGetStringArray(opp, "structure"),
                    Emotion = TryGetString(opp, "emotion"),
                    PsychologicalTrigger = TryGetString(opp, "psychologicalTrigger"),
                    WhyNow = TryGetString(opp, "whyNow"),
                    ContentGap = TryGetString(opp, "contentGap"),
                    DifferentiationStrategy = TryGetString(opp, "differentiationStrategy"),
                    CallToAction = TryGetString(opp, "callToAction"),
                    RiskLevel = TryGetString(opp, "riskLevel"),
                    SupportingVideoIds = TryGetLongArray(opp, "supportingVideoIds"),
                    AiOpportunityScore = TryGetDecimal(opp, "opportunityScore"),
                    AiConfidenceScore = TryGetDecimal(opp, "confidenceScore"),
                };

                var evidence = TryGetStringArray(opp, "evidence");
                if (evidence is not null)
                {
                    draft.Evidence.AddRange(evidence);
                }

                result.OpportunityDrafts.Add(draft);
            }
        }

        return result;
    }

    private List<ContentOpportunity> BuildOpportunities(
        AiAnalysisResult parsed,
        long runId,
        IReadOnlyList<AnalysisCandidate> eligible)
    {
        var result = new List<ContentOpportunity>();
        var validVideoIds = eligible.Select(c => c.VideoId).ToHashSet();

        foreach (var draft in parsed.OpportunityDrafts)
        {
            // Only keep supporting video ids that are actually in this analysis.
            var supportingIds = draft.SupportingVideoIds?
                .Where(id => validVideoIds.Contains(id))
                .ToArray() ?? Array.Empty<long>();

            // Compute the REAL average momentum of the supporting videos from
            // actual performance data. Used as the Trend Momentum scoring component
            // so the scorer stays independent from the AI's opinion.
            draft.AverageSupportingMomentum = ComputeAverageSupportingMomentum(supportingIds, eligible);

            var opportunityScore = _opportunityScorer.Score(draft);
            var confidenceScore = draft.AiConfidenceScore ?? 0m;
            var riskLevel = string.IsNullOrWhiteSpace(draft.RiskLevel) ? "Medium" : draft.RiskLevel!;

            result.Add(new ContentOpportunity
            {
                AnalysisRunId = runId,
                Topic = draft.Topic,
                Angle = draft.Angle ?? string.Empty,
                TargetAudience = draft.TargetAudience,
                Hook = draft.Hook ?? string.Empty,
                Format = draft.Format ?? string.Empty,
                Structure = draft.Structure,
                Emotion = draft.Emotion,
                PsychologicalTrigger = draft.PsychologicalTrigger,
                WhyNow = draft.WhyNow ?? string.Empty,
                ContentGap = draft.ContentGap,
                DifferentiationStrategy = draft.DifferentiationStrategy,
                CallToAction = draft.CallToAction,
                OpportunityScore = Math.Round(opportunityScore, 2),
                ConfidenceScore = Math.Round(confidenceScore, 2),
                RiskLevel = riskLevel,
                SupportingVideoIds = supportingIds,
                Evidence = string.Join(Environment.NewLine, draft.Evidence)
            });
        }

        return result
            .OrderByDescending(o => o.OpportunityScore)
            .ToList();
    }

    // ---------- AI retry helper ----------

    private async Task<AI.ViralAnalysisResponse> CallAiWithRetryAsync(
        AI.ViralAnalysisRequest request,
        long runId,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        var attempts = 0;

        while (attempts == 0 || _retryCalculator.ShouldRetry(attempts - 1))
        {
            if (attempts > 0)
            {
                var delay = _retryCalculator.Calculate(attempts - 1);
                _logger.LogWarning("Retrying AI provider for run {RunId} (attempt {Attempt}) after {Delay:0}s.",
                    runId, attempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }

            try
            {
                var response = await _aiProvider.AnalyzeAsync(request, cancellationToken);
                if (response.Success)
                {
                    return response;
                }

                lastException = new InvalidOperationException(response.ErrorMessage ?? "AI provider failed.");
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            attempts++;
        }

        _logger.LogError(lastException, "AI provider failed for run {RunId} after {Attempts} attempts.",
            runId, attempts);

        throw lastException ?? new InvalidOperationException("AI provider failed after retries.");
    }

    // ---------- Helpers ----------

    private static decimal? ComputeAverageSupportingMomentum(
        long[] supportingIds,
        IReadOnlyList<AnalysisCandidate> eligible)
    {
        if (supportingIds.Length == 0)
        {
            return null;
        }

        var byId = eligible
            .Where(c => c.Performance is not null)
            .ToDictionary(c => c.VideoId, c => c.Performance!.MomentumScore);

        var scores = supportingIds
            .Where(id => byId.ContainsKey(id))
            .Select(id => byId[id])
            .ToList();

        return scores.Count == 0 ? null : Math.Round(scores.Average(), 2);
    }

    private static bool MatchesNiche(TrendingVideo video, string niche)
    {
        var text = string.Join(" ",
            new[] { video.Title, video.Description, string.Join(" ", video.Tags ?? Array.Empty<string>()) });
        return text.Contains(niche, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesKeyword(TrendingVideo video, string keyword)
    {
        var text = string.Join(" ",
            new[] { video.Title, video.Description, string.Join(" ", video.Tags ?? Array.Empty<string>()) });
        return text.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static string Join(string[]? values)
        => values is { Length: > 0 } ? string.Join(", ", values) : "(none)";

    private static string Truncate(string? value, int maxLength)
        => string.IsNullOrWhiteSpace(value) ? string.Empty
            : value.Length <= maxLength ? value : value[..maxLength] + "...";

    private static string FormatNumber(long? value)
        => value switch
        {
            null => "unknown",
            >= 1_000_000_000 => $"{value / 1_000_000_000.0:0.#}B",
            >= 1_000_000 => $"{value / 1_000_000.0:0.#}M",
            >= 1_000 => $"{value / 1_000.0:0.#}K",
            _ => value.Value.ToString()
        };

    private static string FormatNumber(decimal? value)
        => value is null
            ? "unknown"
            : value.Value switch
            {
                >= 1_000_000_000 => $"{value / 1_000_000_000.0m:0.#}B",
                >= 1_000_000 => $"{value / 1_000_000.0m:0.#}M",
                >= 1_000 => $"{value / 1_000.0m:0.#}K",
                _ => $"{value:0.##}"
            };

    private static string FormatPercent(decimal? value)
        => value is null ? "unknown" : $"{value * 100m:0.##}%";

    private static string ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var text = raw.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            return start >= 0 && end > start ? text[start..(end + 1)] : string.Empty;
        }

        if (text.StartsWith('{'))
        {
            var end = text.LastIndexOf('}');
            return end > 0 ? text[..(end + 1)] : text;
        }

        var objectStart = text.IndexOf('{');
        var objectEnd = text.LastIndexOf('}');
        return objectStart >= 0 && objectEnd > objectStart
            ? text[objectStart..(objectEnd + 1)]
            : string.Empty;
    }

    private static string? TryGetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static decimal? TryGetDecimal(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var d))
        {
            return d;
        }

        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string[]? TryGetStringArray(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = value.EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString()!)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();

        return items.Length > 0 ? items : null;
    }

    private static long[]? TryGetLongArray(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = value.EnumerateArray()
            .Select(v => v.TryGetInt64(out var l) ? l : (long?)null)
            .Where(l => l.HasValue)
            .Select(l => l!.Value)
            .ToArray();

        return items.Length > 0 ? items : null;
    }

    private sealed class AiAnalysisResult
    {
        public string? TrendSummary { get; set; }
        public string? MarketObservation { get; set; }
        public decimal? ConfidenceScore { get; set; }
        public List<ContentOpportunityDraft> OpportunityDrafts { get; } = new();
    }
}