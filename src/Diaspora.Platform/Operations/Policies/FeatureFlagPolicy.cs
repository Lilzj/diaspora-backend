namespace Diaspora.Platform.Operations.Policies;

/// <summary>
/// The operational rules a feature flag is evaluated under — not the flag itself.
/// <para>
/// The distinction matters. A flag's <em>state</em> (who sees it, at what percentage) changes many
/// times a day and lives in the runtime store; a flag's <em>policy</em> (how long it may live, what
/// happens when the store is unreachable, whether a kill switch may override it) changes rarely and
/// is reviewed. Keeping them apart is what stops "the flag store is down" from becoming "every
/// feature is on".
/// </para>
/// <para>
/// <b><see cref="FailClosed"/> defaults to true.</b> A flag whose store cannot be read resolves to
/// its off state. The alternative — failing open — means an outage in the flag store silently
/// enables every unfinished feature on the platform at once.
/// </para>
/// </summary>
public sealed record FeatureFlagPolicy : OperationalPolicy
{
    public const string PolicyKind = "feature-flag";

    public FeatureFlagPolicy(string name, int version = 1) : base(name, version)
    {
    }

    public override string Kind => PolicyKind;

    /// <summary>Whether an unreadable store resolves the flag to off.</summary>
    public bool FailClosed { get; init; } = true;

    /// <summary>How long an evaluated result is reused. Zero disables caching.</summary>
    public TimeSpan EvaluationCacheDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether the platform-wide kill switch may force this flag off. Left <see langword="true"/>
    /// for everything except the flags that guard safety controls themselves — a kill switch that
    /// can disable the audit trail is a kill switch that will be used to disable the audit trail.
    /// </summary>
    public bool KillSwitchApplies { get; init; } = true;

    /// <summary>
    /// After this long, the flag is reported as stale on the dashboard. Flags are meant to be
    /// temporary; the ones that are not get reviewed rather than forgotten.
    /// </summary>
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Whether every change to this flag is written to the audit trail. On by default: a flag change
    /// is a production change, and "who turned payments off" is a question that gets asked.
    /// </summary>
    public bool AuditChanges { get; init; } = true;

    /// <summary>
    /// Whether a percentage rollout must hash on a stable subject. On by default — a rollout that
    /// re-rolls per request shows half of a member's screens the new experience and half the old.
    /// </summary>
    public bool RequireStickyRollout { get; init; } = true;

    public override IEnumerable<string> Validate()
    {
        if (EvaluationCacheDuration < TimeSpan.Zero)
        {
            yield return "EvaluationCacheDuration cannot be negative.";
        }

        if (StaleAfter <= TimeSpan.Zero)
        {
            yield return "StaleAfter must be positive.";
        }
    }
}
