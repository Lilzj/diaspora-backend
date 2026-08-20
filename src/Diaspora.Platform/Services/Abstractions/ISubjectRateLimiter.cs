namespace Diaspora.Platform.Services.Abstractions;

/// <summary>
/// The outcome of asking the limiter for permission.
/// </summary>
/// <param name="IsAllowed">Whether the caller may proceed.</param>
/// <param name="RetryAfter">How long until the window frees up. Zero when allowed.</param>
/// <param name="Remaining">Attempts left in the current window.</param>
public readonly record struct RateLimitLease(bool IsAllowed, TimeSpan RetryAfter, int Remaining)
{
    public static RateLimitLease Allowed(int remaining) => new(true, TimeSpan.Zero, remaining);

    public static RateLimitLease Denied(TimeSpan retryAfter) => new(false, retryAfter, 0);
}

/// <summary>
/// Throttles by subject rather than by connection.
/// <para>
/// The transport-level limiter partitions on IP, which is the right defence against one
/// noisy client and the wrong one against a distributed attack: a thousand hosts making one
/// request each at the same phone number never trip an IP limit. This limiter is keyed on
/// the thing being attacked — a phone number, an account, a device — so the cost of an
/// attack scales with the target rather than with the attacker's address pool.
/// </para>
/// <para>
/// The in-memory implementation is correct for a single instance and approximate across
/// several: each instance enforces the limit independently, so N instances permit up to N
/// times the configured rate. That is a real weakening and it is why the interface exists —
/// the Redis implementation is a registration swap, and nothing above this contract changes.
/// </para>
/// </summary>
public interface ISubjectRateLimiter
{
    /// <summary>
    /// Consumes one attempt against <paramref name="key"/>, or refuses.
    /// </summary>
    /// <param name="key">
    /// Stable identifier for what is being protected. Namespace it by purpose —
    /// <c>signin:+2348012345678</c>, not <c>+2348012345678</c> — so a sign-in attempt and a
    /// recovery attempt do not share a budget.
    /// </param>
    /// <param name="permitLimit">Attempts allowed inside the window.</param>
    /// <param name="window">Length of the sliding window.</param>
    ValueTask<RateLimitLease> AcquireAsync(
        string key,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the budget for a key. Called after a successful sign-in so a member who
    /// mistyped their password four times is not still throttled afterwards.
    /// </summary>
    ValueTask ResetAsync(string key, CancellationToken cancellationToken = default);
}
