namespace AIContentFactory.Api.Models.Dtos;

/// <summary>
/// Response returned when resetting all TranscriptUnavailable knowledge
/// extraction queue items back to Pending.
/// </summary>
public sealed class RetryTranscriptUnavailableResponse
{
    /// <summary>Number of queue items that were reset to Pending.</summary>
    public int ResetCount { get; init; }
}