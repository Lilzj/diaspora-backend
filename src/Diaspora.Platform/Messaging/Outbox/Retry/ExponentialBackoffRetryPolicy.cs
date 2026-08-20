using Diaspora.Platform.Messaging.Outbox.Options;
using Microsoft.Extensions.Options;

namespace Diaspora.Platform.Messaging.Outbox.Retry;

/// <summary>
/// The default policy: a few immediate attempts, then capped exponential backoff with jitter,
/// then a dead letter.
/// <para>
/// <b>Backoff is stored, not held.</b> The delay is written into the row as a timestamp rather
/// than waited out in memory, so a restart between attempt three and attempt four does not
/// reset the curve, and a message scheduled ten minutes out is still scheduled ten minutes out
/// after a deployment. An in-memory backoff would quietly become an immediate-retry loop every
/// time a pod recycled.
/// </para>
/// <para>
/// <b>Jitter is not decoration.</b> An outage fails every in-flight message at the same instant,
/// and an unjittered curve marches all of them back in one synchronised wave — which is how a
/// dependency that has just come back gets knocked over again by its own backlog. Jitter is
/// added, never subtracted, so a delay is never shorter than the curve intends.
/// </para>
/// </summary>
public sealed class ExponentialBackoffRetryPolicy : IOutboxRetryPolicy
{
    private readonly OutboxRetryOptions _options;
    private readonly PoisonMessageDetector _poison;

    public ExponentialBackoffRetryPolicy(IOptions<OutboxOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value.Retry;
        _poison = new PoisonMessageDetector(_options);
    }

    public OutboxRetryDecision Decide(OutboxFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        if (_poison.IsPoison(failure.Exception, out var poisonReason))
        {
            return OutboxRetryDecision.DeadLetter(failure.Now, poisonReason);
        }

        // AttemptCount is the number already made; this failure is attempt AttemptCount + 1.
        var attemptsMade = failure.AttemptCount + 1;

        if (attemptsMade >= _options.MaxAttempts)
        {
            return OutboxRetryDecision.DeadLetter(
                failure.Now, Model.OutboxDeadLetterReason.MaxAttemptsExceeded);
        }

        return OutboxRetryDecision.Retry(failure.Now + DelayFor(attemptsMade));
    }

    /// <summary>
    /// The delay before the attempt following <paramref name="attemptsMade"/>.
    /// <para>
    /// Computed rather than tabulated so the curve stays correct when the options change, and
    /// exposed for the unit suite, which asserts the shape directly — a backoff curve is not
    /// something to discover is wrong from production latency graphs.
    /// </para>
    /// </summary>
    public TimeSpan DelayFor(int attemptsMade)
    {
        if (attemptsMade <= _options.ImmediateAttempts)
        {
            return TimeSpan.Zero;
        }

        // The first backing-off attempt gets exactly InitialBackoffSeconds.
        var step = attemptsMade - _options.ImmediateAttempts - 1;

        // Capped before the shift, not after: 2^60 seconds overflows a double into infinity and
        // then into an unrepresentable DateTimeOffset, which throws inside the failure path —
        // the one place an exception turns a retry into a lost message.
        var doublings = Math.Min(step, 30);

        var seconds = _options.InitialBackoffSeconds * Math.Pow(2, doublings);
        seconds = Math.Min(seconds, _options.MaxBackoffSeconds);

        if (_options.BackoffJitter > 0)
        {
            // Random.Shared is thread-safe and this is not a security decision — only a smear
            // across a herd of retries. A cryptographic source here would be cost without
            // benefit.
            seconds += seconds * _options.BackoffJitter * Random.Shared.NextDouble();
        }

        return TimeSpan.FromSeconds(seconds);
    }
}
