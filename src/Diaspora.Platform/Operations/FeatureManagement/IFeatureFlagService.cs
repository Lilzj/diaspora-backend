namespace Diaspora.Platform.Operations.FeatureManagement;

/// <summary>
/// Reads and evaluates feature flags.
/// <para>
/// <b>Business modules do not consume this.</b> B17-R3 fails a build where a domain type, a command
/// handler or an aggregate references it: a flag inside domain logic makes the domain's behaviour
/// depend on an operational lever, which means the same input produces different results on two
/// replicas mid-rollout and no test can pin it. Flags belong at composition — choosing an
/// implementation, mounting an endpoint, enabling a worker — where the choice is made once and is
/// visible.
/// </para>
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>Whether a flag is on for the current caller.</summary>
    ValueTask<bool> IsEnabledAsync(string flag, CancellationToken cancellationToken = default);

    /// <summary>Whether a flag is on for a specific context.</summary>
    ValueTask<bool> IsEnabledAsync(
        string flag, FeatureFlagContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// The full evaluation, including which rule decided.
    /// <para>
    /// What the operational dashboard shows and what an operator needs when a flag is not behaving:
    /// "off" is not an explanation, and "excluded by the country rule at position 2" is.
    /// </para>
    /// </summary>
    ValueTask<FeatureFlagEvaluation> EvaluateAsync(
        string flag, FeatureFlagContext context, CancellationToken cancellationToken = default);

    /// <summary>Every declared flag.</summary>
    Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Changes flags. Separate from reading them, because reading is on every request path and changing
/// is a staff operation behind a permission.
/// </summary>
public interface IFeatureFlagAdministration
{
    /// <summary>Creates or replaces a flag.</summary>
    Task SetAsync(
        FeatureFlag flag, string? changedBy, string? reason, CancellationToken cancellationToken = default);

    /// <summary>Turns a flag on or off without touching its rules.</summary>
    Task SetStateAsync(
        string flag, bool enabled, string? changedBy, string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a flag. Its declaration will recreate it at the next startup if a module owns it.</summary>
    Task RemoveAsync(string flag, string? changedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Engages the platform-wide kill switch.
    /// <para>
    /// Every flag whose policy has <c>KillSwitchApplies</c> resolves to off until it is released.
    /// Flags under the safety-control policy — moderation, audit, rate limiting — are deliberately not
    /// reachable: a kill switch that could disable the audit trail is a kill switch that will one day
    /// be used to disable the audit trail.
    /// </para>
    /// </summary>
    Task EngageKillSwitchAsync(string engagedBy, string reason, CancellationToken cancellationToken = default);

    /// <summary>Releases the kill switch.</summary>
    Task ReleaseKillSwitchAsync(string releasedBy, CancellationToken cancellationToken = default);

    /// <summary>Whether the kill switch is engaged, and why.</summary>
    Task<KillSwitchState> GetKillSwitchAsync(CancellationToken cancellationToken = default);

    /// <summary>The change history, newest first.</summary>
    Task<IReadOnlyList<FeatureFlagChange>> HistoryAsync(
        string? flag = null, int limit = 100, CancellationToken cancellationToken = default);
}

/// <summary>The kill switch's state.</summary>
/// <param name="IsEngaged">Whether it is on.</param>
/// <param name="EngagedAt">When.</param>
/// <param name="EngagedBy">Who.</param>
/// <param name="Reason">Why. Required to engage — an unexplained kill switch is an unresolvable incident.</param>
public sealed record KillSwitchState(
    bool IsEngaged,
    DateTimeOffset? EngagedAt,
    string? EngagedBy,
    string? Reason)
{
    public static KillSwitchState Released { get; } = new(false, null, null, null);
}

/// <summary>
/// Declares a module's flags to the platform.
/// <para>
/// Declared rather than created through the administration API so that a flag exists — with its
/// description, its owner and its default — from the first boot of the code that reads it. A flag
/// that only appears once somebody creates it resolves to its fallback in the meantime, which is the
/// same failure as a typo and just as silent.
/// </para>
/// </summary>
public interface IFeatureFlagContributor
{
    string Module { get; }

    IEnumerable<FeatureFlag> Flags { get; }
}
