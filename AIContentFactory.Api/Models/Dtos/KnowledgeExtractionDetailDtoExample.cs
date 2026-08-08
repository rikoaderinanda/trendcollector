using AIContentFactory.Api.Models.Entities;
using Swashbuckle.AspNetCore.Filters;

namespace AIContentFactory.Api.Models.Dtos;

/// <summary>
/// Swagger example for full knowledge extraction detail of a video.
/// </summary>
public sealed class KnowledgeExtractionDetailDtoExample : IExamplesProvider<KnowledgeExtractionDetailDto>
{
    public KnowledgeExtractionDetailDto GetExamples()
    {
        return new KnowledgeExtractionDetailDto
        {
            Metadata = new TrendingVideoMetadata
            {
                Id = 42,
                PlatformId = 1,
                PlatformVideoId = "dQw4w9WgXcQ",
                ChannelId = 5,
                Title = "How AI is Changing Content Creation",
                Description = "In this video we explore how AI agents are transforming the content creation industry.",
                Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                PublishedAt = DateTimeOffset.UtcNow.AddDays(-7),
                Duration = "PT12M34S",
                Category = "Science & Technology",
                Tags = new[] { "AI", "content creation", "automation" },
                Language = "id",
                CaptionAvailable = true
            },
            Transcript = new VideoTranscript
            {
                Id = 42,
                VideoId = 42,
                Transcript = "Welcome back to the channel. Today we're looking at how AI is changing...",
                Language = "id",
                Source = "youtube_captions",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-7)
            },
            Knowledge = new VideoKnowledge
            {
                Id = 42,
                VideoId = 42,
                Summary = "The video explains how AI agents are transforming content creation workflows.",
                MainTopic = "AI in Content Creation",
                Keywords = new[] { "AI content", "automation", "creator economy" },
                TargetAudience = "Content creators, marketers, AI enthusiasts",
                Tone = "Informative",
                Hook = "Imagine having an AI agent that creates your entire content pipeline.",
                ContentStructure = new[] { "Hook", "Problem", "Solution", "Demo", "CTA" },
                CallToAction = "Subscribe and turn on notifications for more AI content.",
                ImportantPoints = new[] { "AI can automate research", "AI can summarize videos" },
                LearningNotes = new[] { "Knowledge extraction is key for AI agents" },
                InterestingFacts = new[] { "AI agents can process thousands of videos" },
                PsychologicalTriggers = new[] { "Curiosity", "FOMO" },
                StoryPattern = "Problem → Solution",
                ContentType = "Educational",
                DifficultyLevel = "Intermediate",
                Language = "id",
                Emotion = "Curiosity",
                CuriosityScore = 88,
                EducationalValue = 90,
                EntertainmentValue = 65,
                EngagementTechniques = new[] { "Questions", "Pacing", "Visuals" },
                RetentionStrategy = "Fast pacing with questions",
                SuggestedImprovements = new[] { "Add real examples" },
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-7),
                UpdatedAt = DateTimeOffset.UtcNow
            },
            Queue = new KnowledgeExtractionQueue
            {
                Id = 17,
                VideoId = 42,
                Status = QueueStatus.Completed,
                Priority = 5,
                RetryCount = 0,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                FinishedAt = DateTimeOffset.UtcNow.AddMinutes(-9),
                DurationMs = 60000,
                ErrorMessage = null,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-6),
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };
    }
}