namespace Diaspora.Platform.Operations.Policies;

/// <summary>
/// How a transient failure is retried.
/// <para>
/// One shape for the whole platform. Before B17 there were three: the outbox's own exponential
/// backoff, EF Core's execution strategy, and a hand-rolled loop in every adapter that talked to
/// something remote. The outbox keeps its implementation — it retries across process restarts,
/// which an in-memory executor cannot — but it now derives its numbers from a policy of this
/// shape, so an operator tuning retries tunes them in one vocabulary.
/// </para>
/// <para>
/// <b>Jitter is on by default and that is not a detail.</b> Without it, a hundred consumers that
/// fail against one dependency at the same instant retry at the same instant, forever, in
/// lock-step. The dependency recovers and is immediately knocked over by the herd it created.
/// </para>
/// </summary>
public sealed record RetryPolicy : OperationalPolicy
{
    public const string PolicyKind = "retry";

    public RetryPolicy(string name, int version = 1) : base(name, version)
    {
    }

    public override string Kind => PolicyKind;

    /// <summary>Attempts after the first. <c>0</c> disables retry without disabling the policy.</summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>The wait before the first retry. Doubled by <see cref="BackoffMultiplier"/> thereafter.</summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Ceiling on the computed delay, applied before jitter.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Growth factor per attempt. <c>1.0</c> gives a constant delay.</summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>
    /// Fraction of the computed delay to randomise, in <c>[0, 1]</c>. <c>0.2</c> spreads a herd
    /// over ±20% of the delay, which is enough to break lock-step without meaningfully changing
    /// how long a caller waits.
    /// </summary>
    public double JitterRatio { get; init; } = 0.2;

    /// <summary>
    /// Total time the whole retry sequence may consume. Reached first, the sequence stops even if
    /// attempts remain — a caller behind a 30-second gateway timeout gains nothing from a fourth
    /// attempt at second 45.
    /// </summary>
    public TimeSpan? OverallTimeout { get; init; }

    /// <summary>
    /// Consecutive failures after which the message or operation is treated as poison and stops
    /// being retried at all. Distinct from <see cref="MaxAttempts"/>, which bounds one call;
    /// this bounds a message's whole life across process restarts.
    /// </summary>
    public int PoisonThreshold { get; init; } = 10;

    public override IEnumerable<string> Validate()
    {
        if (MaxAttempts < 0)
        {
            yield return "MaxAttempts cannot be negative.";
        }

        if (InitialDelay < TimeSpan.Zero)
        {
            yield return "InitialDelay cannot be negative.";
        }

        if (MaxDelay < InitialDelay)
        {
            yield return "MaxDelay is below InitialDelay, so the ceiling would clamp the first retry.";
        }

        if (BackoffMultiplier < 1.0)
        {
            yield return "BackoffMultiplier below 1.0 makes each retry sooner than the last.";
        }

        if (JitterRatio is < 0 or > 1)
        {
            yield return "JitterRatio must be between 0 and 1.";
        }

        if (OverallTimeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            yield return "OverallTimeout must be positive when set.";
        }

        if (PoisonThreshold < 1)
        {
            yield return "PoisonThreshold must be at least 1.";
        }
    }
}
