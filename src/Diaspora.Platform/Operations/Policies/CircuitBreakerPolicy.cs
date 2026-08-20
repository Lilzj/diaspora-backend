namespace Diaspora.Platform.Operations.Policies;

/// <summary>
/// When to stop calling a dependency that is failing.
/// <para>
/// A breaker exists to protect the caller, not the callee. Fifty threads waiting thirty seconds
/// each on a payment gateway that is down do not make the gateway recover; they exhaust the
/// platform's own connection pool and take out everything else that needed a thread. Opening the
/// circuit converts a slow failure into a fast one, which is the only kind a request can survive.
/// </para>
/// <para>
/// <b>Failure ratio, not failure count.</b> Ten failures mean nothing without a denominator: on a
/// dependency handling ten thousand calls a minute it is noise, and on one handling twelve it is
/// a total outage. <see cref="MinimumThroughput"/> is what stops a ratio computed over three calls
/// from tripping the breaker on a quiet endpoint.
/// </para>
/// </summary>
public sealed record CircuitBreakerPolicy : OperationalPolicy
{
    public const string PolicyKind = "circuit-breaker";

    public CircuitBreakerPolicy(string name, int version = 1) : base(name, version)
    {
    }

    public override string Kind => PolicyKind;

    /// <summary>Fraction of calls in the sampling window that must fail to open the circuit.</summary>
    public double FailureRatio { get; init; } = 0.5;

    /// <summary>The rolling window the ratio is computed over.</summary>
    public TimeSpan SamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Calls the window must contain before the ratio is trusted. Below this the breaker stays
    /// closed no matter what the ratio says.
    /// </summary>
    public int MinimumThroughput { get; init; } = 10;

    /// <summary>How long the circuit stays open before admitting one trial call.</summary>
    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Trial calls admitted while half-open. One is the safe default: a dependency that just failed
    /// should not receive a burst the instant it looks alive.
    /// </summary>
    public int HalfOpenPermittedCalls { get; init; } = 1;

    /// <summary>
    /// Whether a call that exceeds <see cref="CallTimeout"/> counts as a failure. Almost always
    /// yes — the failure mode that hurts most is a dependency that answers slowly rather than not
    /// at all, and a breaker that only counts exceptions never sees it.
    /// </summary>
    public bool TimeoutCountsAsFailure { get; init; } = true;

    /// <summary>Per-call ceiling. <see langword="null"/> defers to the caller's own timeout.</summary>
    public TimeSpan? CallTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public override IEnumerable<string> Validate()
    {
        if (FailureRatio is <= 0 or > 1)
        {
            yield return "FailureRatio must be greater than 0 and at most 1.";
        }

        if (SamplingDuration <= TimeSpan.Zero)
        {
            yield return "SamplingDuration must be positive.";
        }

        if (MinimumThroughput < 1)
        {
            yield return "MinimumThroughput must be at least 1, or the ratio is computed over nothing.";
        }

        if (BreakDuration <= TimeSpan.Zero)
        {
            yield return "BreakDuration must be positive, or the circuit reopens instantly.";
        }

        if (HalfOpenPermittedCalls < 1)
        {
            yield return "HalfOpenPermittedCalls must be at least 1, or the circuit can never close.";
        }

        if (CallTimeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            yield return "CallTimeout must be positive when set.";
        }
    }
}
