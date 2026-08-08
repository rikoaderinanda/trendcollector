namespace AIContentFactory.Api.Models.Entities;

/// <summary>
/// Lifecycle status of a knowledge extraction queue item.
/// </summary>
public enum QueueStatus
{
    /// <summary>Waiting to be processed by the background worker.</summary>
    Pending,

    /// <summary>Currently being processed.</summary>
    Running,

    /// <summary>Knowledge extracted and persisted successfully.</summary>
    Completed,

    /// <summary>Failed after exhausting all retry attempts.</summary>
    Failed,

    /// <summary>Video has no available transcript (captions disabled / not available).</summary>
    TranscriptUnavailable
}