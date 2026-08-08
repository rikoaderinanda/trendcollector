namespace AIContentFactory.Api.Exceptions;

/// <summary>
/// Base exception for trend collector errors.
/// </summary>
public class TrendCollectorException : Exception
{
    public TrendCollectorException(string message) : base(message) { }
    public TrendCollectorException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when the YouTube API returns a quota-related error (429 / dailyLimitExceeded).
/// Not retryable within the same day.
/// </summary>
public sealed class YouTubeQuotaExceededException : TrendCollectorException
{
    public YouTubeQuotaExceededException(string message) : base(message) { }
}

/// <summary>
/// Thrown when the YouTube API key is invalid or forbidden (400 / 403).
/// Not retryable and not switch-able to Tracking Mode.
/// </summary>
public sealed class YouTubeApiKeyInvalidException : TrendCollectorException
{
    public YouTubeApiKeyInvalidException(string message) : base(message) { }
}

/// <summary>
/// Thrown for transient YouTube API failures (5xx, rate limits, network issues).
/// Safe to retry.
/// </summary>
public sealed class YouTubeTransientException : TrendCollectorException
{
    public YouTubeTransientException(string message) : base(message) { }
    public YouTubeTransientException(string message, Exception innerException) : base(message, innerException) { }
}