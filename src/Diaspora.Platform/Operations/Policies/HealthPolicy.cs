namespace Diaspora.Platform.Operations.Policies;

/// <summary>
/// What a module's health check is allowed to cost, and what its failure means.
/// <para>
/// The second half is the part that matters. A health check that reports unhealthy has told the
/// orchestrator to take the instance out of rotation; a check on a dependency the platform can
/// operate without has just turned a degraded feature into an outage. B17 makes that a declared
/// property of the check rather than a decision buried in each one — <see cref="Critical"/> is
/// false by default, so a new dependency check degrades until somebody decides otherwise.
/// </para>
/// </summary>
public sealed record HealthPolicy : OperationalPolicy
{
    public const string PolicyKind = "health";

    public HealthPolicy(string name, int version = 1) : base(name, version)
    {
    }

    public override string Kind => PolicyKind;

    /// <summary>
    /// Whether this check's failure makes the instance unready. <see langword="false"/> degrades:
    /// the instance keeps serving and the dashboard shows the dependency as impaired.
    /// </summary>
    public bool Critical { get; init; }

    /// <summary>
    /// Ceiling on how long the check may take. A readiness probe that hangs is worse than one that
    /// fails: the orchestrator's own probe timeout fires, the reason is lost, and every check in
    /// the report is reported as unknown.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Time a result is reused before the check runs again. Probes arrive every few seconds from
    /// several sources; a dependency check that actually opens a connection should not run on every
    /// one of them.
    /// </summary>
    public TimeSpan CacheDuration { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Consecutive failures before the aggregate status changes. One is right for a cheap local
    /// check and wrong for a network dependency, where a single dropped packet should not remove
    /// an instance from rotation.
    /// </summary>
    public int FailureThreshold { get; init; } = 1;

    /// <summary>Consecutive successes before an unhealthy check is believed again.</summary>
    public int RecoveryThreshold { get; init; } = 1;

    public override IEnumerable<string> Validate()
    {
        if (Timeout <= TimeSpan.Zero)
        {
            yield return "Timeout must be positive.";
        }

        if (CacheDuration < TimeSpan.Zero)
        {
            yield return "CacheDuration cannot be negative.";
        }

        if (FailureThreshold < 1)
        {
            yield return "FailureThreshold must be at least 1.";
        }

        if (RecoveryThreshold < 1)
        {
            yield return "RecoveryThreshold must be at least 1.";
        }
    }
}
