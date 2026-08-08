namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Structured knowledge extracted from a video by the AI provider.
/// This is the reusable knowledge asset consumed by future agents.
/// </summary>
public sealed class VideoKnowledge
{
    public long Id { get; set; }

    /// <summary>FK to trending_videos.id.</summary>
    public long VideoId { get; set; }

    public string? Summary { get; set; }
    public string? MainTopic { get; set; }
    public string[]? Keywords { get; set; }
    public string? TargetAudience { get; set; }
    public string? Tone { get; set; }

    /// <summary>Opening hook of the video.</summary>
    public string? Hook { get; set; }

    public string[]? ContentStructure { get; set; }
    public string? CallToAction { get; set; }
    public string[]? ImportantPoints { get; set; }
    public string[]? LearningNotes { get; set; }
    public string[]? InterestingFacts { get; set; }
    public string[]? PsychologicalTriggers { get; set; }
    public string? StoryPattern { get; set; }
    public string? ContentType { get; set; }
    public string? DifficultyLevel { get; set; }
    public string? Language { get; set; }

    /// <summary>Dominant emotion conveyed, e.g. "Curiosity".</summary>
    public string? Emotion { get; set; }

    /// <summary>Curiosity score from 1 to 100.</summary>
    public int? CuriosityScore { get; set; }

    /// <summary>Educational value score from 1 to 100.</summary>
    public int? EducationalValue { get; set; }

    /// <summary>Entertainment value score from 1 to 100.</summary>
    public int? EntertainmentValue { get; set; }

    public string[]? EngagementTechniques { get; set; }

    public string? RetentionStrategy { get; set; }
    public string[]? SuggestedImprovements { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}