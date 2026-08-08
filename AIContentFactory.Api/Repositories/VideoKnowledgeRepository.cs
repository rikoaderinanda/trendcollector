using Dapper;
using AIContentFactory.Api.Data;
using AIContentFactory.Api.Models.Entities;

namespace AIContentFactory.Api.Repositories;

/// <inheritdoc cref="IVideoKnowledgeRepository" />
public sealed class VideoKnowledgeRepository : IVideoKnowledgeRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public VideoKnowledgeRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task UpsertAsync(VideoKnowledge knowledge, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO video_knowledge (
                video_id, summary, main_topic, keywords, target_audience, tone,
                hook, content_structure, call_to_action, important_points,
                learning_notes, interesting_facts, psychological_triggers,
                story_pattern, content_type, difficulty_level, language,
                emotion, curiosity_score,
                educational_value, entertainment_value, engagement_techniques,
                retention_strategy, suggested_improvements
            )
            VALUES (
                @VideoId, @Summary, @MainTopic, @Keywords, @TargetAudience, @Tone,
                @Hook, @ContentStructure, @CallToAction, @ImportantPoints,
                @LearningNotes, @InterestingFacts, @PsychologicalTriggers,
                @StoryPattern, @ContentType, @DifficultyLevel, @Language,
                @Emotion, @CuriosityScore,
                @EducationalValue, @EntertainmentValue, @EngagementTechniques,
                @RetentionStrategy, @SuggestedImprovements
            )
            ON CONFLICT (video_id) DO UPDATE
            SET summary                   = EXCLUDED.summary,
                main_topic                = EXCLUDED.main_topic,
                keywords                  = EXCLUDED.keywords,
                target_audience           = EXCLUDED.target_audience,
                tone                      = EXCLUDED.tone,
                hook                      = EXCLUDED.hook,
                content_structure         = EXCLUDED.content_structure,
                call_to_action            = EXCLUDED.call_to_action,
                important_points          = EXCLUDED.important_points,
                learning_notes            = EXCLUDED.learning_notes,
                interesting_facts         = EXCLUDED.interesting_facts,
                psychological_triggers    = EXCLUDED.psychological_triggers,
                story_pattern             = EXCLUDED.story_pattern,
                content_type              = EXCLUDED.content_type,
                difficulty_level          = EXCLUDED.difficulty_level,
                language                  = EXCLUDED.language,
                emotion                   = EXCLUDED.emotion,
                curiosity_score           = EXCLUDED.curiosity_score,
                educational_value         = EXCLUDED.educational_value,
                entertainment_value       = EXCLUDED.entertainment_value,
                engagement_techniques     = EXCLUDED.engagement_techniques,
                retention_strategy        = EXCLUDED.retention_strategy,
                suggested_improvements    = EXCLUDED.suggested_improvements,
                updated_at                = now();
            """;

        await connection.ExecuteAsync(sql, knowledge, commandTimeout: 30);
    }

    public async Task<VideoKnowledge?> GetByVideoIdAsync(long videoId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                id                       AS Id,
                video_id                 AS VideoId,
                summary                  AS Summary,
                main_topic               AS MainTopic,
                keywords                 AS Keywords,
                target_audience          AS TargetAudience,
                tone                     AS Tone,
                hook                     AS Hook,
                content_structure        AS ContentStructure,
                call_to_action           AS CallToAction,
                important_points         AS ImportantPoints,
                learning_notes           AS LearningNotes,
                interesting_facts        AS InterestingFacts,
                psychological_triggers   AS PsychologicalTriggers,
                story_pattern            AS StoryPattern,
                content_type             AS ContentType,
                difficulty_level         AS DifficultyLevel,
                language                 AS Language,
                emotion                  AS Emotion,
                curiosity_score          AS CuriosityScore,
                educational_value        AS EducationalValue,
                entertainment_value      AS EntertainmentValue,
                engagement_techniques    AS EngagementTechniques,
                retention_strategy       AS RetentionStrategy,
                suggested_improvements   AS SuggestedImprovements,
                created_at               AS CreatedAt,
                updated_at               AS UpdatedAt
            FROM video_knowledge
            WHERE video_id = @VideoId;
            """;

        return await connection.QuerySingleOrDefaultAsync<VideoKnowledge>(sql,
            new { VideoId = videoId }, commandTimeout: 30);
    }
}