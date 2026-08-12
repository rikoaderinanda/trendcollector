namespace AIContentFactory.Api.Transcript;

/// <summary>
/// Thrown when a transcript fetch fails due to a transient condition that may
/// succeed on a later retry (e.g. HTTP 429 Rate Limited, 5xx server errors,
/// timeouts). Callers treat this as "retryable" rather than permanently
/// unavailable, so the job avoids the terminal TranscriptUnavailable state.
/// </summary>
public sealed class TranscriptTransientException : Exception
{
    public TranscriptTransientException(string message)
        : base(message)
    {
    }

    public TranscriptTransientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}