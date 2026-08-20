namespace Diaspora.Application.Exceptions;

public sealed class RateLimitException : Exception
{
    public int RetryAfterSeconds { get; }

    public RateLimitException(int retryAfterSeconds, string message = "Too many requests.")
        : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
