using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diaspora.Operations.Telemetry;
using Diaspora.Platform.Operations.FeatureManagement;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Telemetry;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.FeatureManagement;

/// <summary>
/// Evaluates, stores and audits feature flags.
/// <para>
/// One class rather than three, because the read path and the write path share the state and
/// splitting them would mean either a shared mutable store passed between them or two copies that
/// can disagree. The interfaces are separate — reading is on every request path, writing is a staff
/// operation behind a permission — and that is where the separation matters.
/// </para>
/// <para>
/// <b>Flags are held in memory and rebuilt from declarations at every startup.</b> A change made
/// through the administration API is lost on restart, which is the honest state of a platform with no
/// flag store, and it is why every flag has a declared default that is the shipped answer. The seam
/// for a durable store is <see cref="IFeatureFlagContributor"/> plus this class: a store-backed
/// implementation reads declarations at startup, overlays persisted state, and nothing that consumes
/// a flag changes.
/// </para>
/// </summary>
public sealed class FeatureFlagService : IFeatureFlagService, IFeatureFlagAdministration
{
    private const int HistoryLimit = 500;

    private static readonly FeatureFlagPolicy FallbackPolicy = new(OperationalPolicyNames.FeatureFlag.Default);

    private readonly ConcurrentDictionary<string, FeatureFlag> _flags = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<FeatureFlagChange> _history = new();
    private readonly IOperationalPolicyCatalog _policies;
    private readonly IClock _clock;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<FeatureFlagService> _logger;

    private KillSwitchState _killSwitch = KillSwitchState.Released;

    public FeatureFlagService(
        IEnumerable<IFeatureFlagContributor> contributors,
        IOperationalPolicyCatalog policies,
        IClock clock,
        IHostEnvironment environment,
        ILogger<FeatureFlagService> logger)
    {
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        _policies = policies;
        _clock = clock;
        _environment = environment;
        _logger = logger;

        foreach (var contributor in contributors)
        {
            foreach (var flag in contributor.Flags ?? [])
            {
                if (!_flags.TryAdd(flag.Name, flag))
                {
                    // Two modules declaring one flag is the same class of error as two modules
                    // declaring one permission key, and the platform refuses that too: whichever
                    // assembly the scan reached first would silently own the name.
                    throw new InvalidOperationException(
                        $"Feature flag '{flag.Name}' is declared by more than one module — " +
                        $"'{_flags[flag.Name].Module}' and '{contributor.Module}'.");
                }
            }
        }
    }

    // ─── Reading ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates with no subject beyond the environment.
    /// <para>
    /// <b>This singleton has no way to know who is calling, and does not pretend to.</b> An earlier
    /// draft injected <c>ICurrentUser</c> here and the container's scope validation refused to build:
    /// a scoped service captured by a singleton is resolved once, at startup, and every later call
    /// sees whoever happened to be signed in then. <see cref="ScopedFeatureFlagService"/> is the
    /// per-request surface and is what the container hands to a caller asking for
    /// <see cref="IFeatureFlagService"/>.
    /// </para>
    /// </summary>
    public ValueTask<bool> IsEnabledAsync(string flag, CancellationToken cancellationToken = default) =>
        IsEnabledAsync(flag, new FeatureFlagContext(Environment: _environment.EnvironmentName), cancellationToken);

    public async ValueTask<bool> IsEnabledAsync(
        string flag, FeatureFlagContext context, CancellationToken cancellationToken = default)
    {
        var evaluation = await EvaluateAsync(flag, context, cancellationToken).ConfigureAwait(false);

        return evaluation.IsEnabled;
    }

    public ValueTask<FeatureFlagEvaluation> EvaluateAsync(
        string flag, FeatureFlagContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flag);
        cancellationToken.ThrowIfCancellationRequested();

        var evaluation = Evaluate(flag, context);

        OperationsTelemetry.FlagEvaluations.Add(1,
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Operation, flag),
            new KeyValuePair<string, object?>(
                PlatformTelemetryNames.Attributes.Outcome, evaluation.IsEnabled ? "enabled" : "disabled"));

        return ValueTask.FromResult(evaluation);
    }

    public Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FeatureFlag> flags =
            [.. _flags.Values.OrderBy(flag => flag.Name, StringComparer.Ordinal)];

        return Task.FromResult(flags);
    }

    // ─── Administration ──────────────────────────────────────────────────────────────────

    public Task SetAsync(
        FeatureFlag flag, string? changedBy, string? reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flag);

        var before = _flags.GetValueOrDefault(flag.Name);
        var updated = flag with { UpdatedAt = _clock.UtcNow, UpdatedBy = changedBy };

        _flags[flag.Name] = updated;

        RecordChange(new FeatureFlagChange(flag.Name, _clock.UtcNow, changedBy, before, updated, reason));

        return Task.CompletedTask;
    }

    public Task SetStateAsync(
        string flag,
        bool enabled,
        string? changedBy,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flag);

        if (!_flags.TryGetValue(flag, out var existing))
        {
            throw new KeyNotFoundException($"No feature flag named '{flag}' is declared.");
        }

        // Replaces the whole rule set with one "everyone" rule. An operator flipping a flag during an
        // incident means "on for everybody" or "off for everybody"; leaving a percentage rule in place
        // beneath the switch would make the result depend on rules nobody is looking at.
        var updated = existing with
        {
            DefaultState = enabled,
            Rules = [RolloutRule.Everyone(enabled)],
            UpdatedAt = _clock.UtcNow,
            UpdatedBy = changedBy
        };

        _flags[flag] = updated;

        RecordChange(new FeatureFlagChange(flag, _clock.UtcNow, changedBy, existing, updated, reason));

        _logger.LogWarning(
            "Feature flag {Flag} was turned {State} by {ChangedBy}. Reason: {Reason}",
            flag, enabled ? "on" : "off", changedBy ?? "the platform", reason ?? "(none given)");

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string flag, string? changedBy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flag);

        if (_flags.TryRemove(flag, out var removed))
        {
            RecordChange(new FeatureFlagChange(flag, _clock.UtcNow, changedBy, removed, null, null));
        }

        return Task.CompletedTask;
    }

    public Task EngageKillSwitchAsync(
        string engagedBy, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engagedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        _killSwitch = new KillSwitchState(true, _clock.UtcNow, engagedBy, reason);

        OperationsTelemetry.KillSwitchEngaged.Add(1);

        _logger.LogCritical(
            "The platform kill switch was engaged by {EngagedBy}. Every flag under a policy that " +
            "permits it now resolves to off. Reason: {Reason}",
            engagedBy, reason);

        return Task.CompletedTask;
    }

    public Task ReleaseKillSwitchAsync(string releasedBy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(releasedBy);

        _killSwitch = KillSwitchState.Released;

        _logger.LogWarning("The platform kill switch was released by {ReleasedBy}.", releasedBy);

        return Task.CompletedTask;
    }

    public Task<KillSwitchState> GetKillSwitchAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_killSwitch);

    public Task<IReadOnlyList<FeatureFlagChange>> HistoryAsync(
        string? flag = null, int limit = 100, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        IReadOnlyList<FeatureFlagChange> changes =
        [
            .. _history
                .Where(change => flag is null || string.Equals(change.Flag, flag, StringComparison.Ordinal))
                .OrderByDescending(change => change.ChangedAt)
                .Take(limit)
        ];

        return Task.FromResult(changes);
    }

    /// <summary>Flags whose policy says they should have been retired by now.</summary>
    public IReadOnlyList<FeatureFlag> StaleFlags()
    {
        var now = _clock.UtcNow;

        return
        [
            .. _flags.Values
                .Where(flag => now - flag.CreatedAt > PolicyFor(flag).StaleAfter)
                .OrderBy(flag => flag.CreatedAt)
        ];
    }

    // ─── Evaluation ──────────────────────────────────────────────────────────────────────

    private FeatureFlagEvaluation Evaluate(string name, FeatureFlagContext context)
    {
        if (!_flags.TryGetValue(name, out var flag))
        {
            // An undeclared flag is off. The alternative — on — means a typo in a flag name silently
            // enables an unfinished feature, and nothing anywhere reports it.
            return new FeatureFlagEvaluation(name, false, "undeclared", null);
        }

        var policy = PolicyFor(flag);

        if (_killSwitch.IsEngaged && policy.KillSwitchApplies)
        {
            return new FeatureFlagEvaluation(name, false, "kill-switch", null);
        }

        var environment = context.Environment ?? _environment.EnvironmentName;

        for (var index = 0; index < flag.Rules.Count; index++)
        {
            var rule = flag.Rules[index];

            if (!Matches(rule, flag, context, environment))
            {
                continue;
            }

            var position = index.ToString(CultureInfo.InvariantCulture);

            return new FeatureFlagEvaluation(
                name, rule.Enabled, $"rule[{position}]:{rule.Dimension}", rule.Dimension);
        }

        return new FeatureFlagEvaluation(name, flag.DefaultState, "default", null);
    }

    private static bool Matches(
        RolloutRule rule, FeatureFlag flag, FeatureFlagContext context, string environment) =>
        rule.Dimension switch
        {
            RolloutDimension.Everyone => true,
            RolloutDimension.Country => Contains(rule.Values, context.CountryCode),
            RolloutDimension.Tenant => Contains(rule.Values, context.TenantId),
            RolloutDimension.User => Contains(rule.Values, context.UserId),
            RolloutDimension.Environment => Contains(rule.Values, environment),
            RolloutDimension.Role => context.Roles is { Count: > 0 } roles &&
                roles.Any(role => Contains(rule.Values, role)),
            RolloutDimension.Percentage => InBucket(flag.Name, context.UserId, rule.Percentage),
            _ => false
        };

    private static bool Contains(IReadOnlyList<string> values, string? candidate) =>
        candidate is not null &&
        values.Any(value => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether a subject falls in a percentage bucket.
    /// <para>
    /// <b>Hashed on the flag name and the subject together</b>, which is the detail that makes a
    /// percentage rollout usable. Hashing the subject alone would put the same unlucky 5% of members
    /// in the first bucket of every flag on the platform — so the same small group would receive every
    /// experiment, and their experience would diverge from everyone else's permanently.
    /// </para>
    /// <para>
    /// A subject with no id is never in the bucket. Randomising would make an anonymous caller's answer
    /// change between requests, which is the non-sticky rollout the policy explicitly forbids.
    /// </para>
    /// </summary>
    private static bool InBucket(string flagName, string? subject, int percentage)
    {
        if (percentage <= 0 || string.IsNullOrEmpty(subject))
        {
            return false;
        }

        if (percentage >= 100)
        {
            return true;
        }

        var bytes = Encoding.UTF8.GetBytes($"{flagName}:{subject}");
        var hash = SHA256.HashData(bytes);
        var bucket = BitConverter.ToUInt32(hash, 0) % 100;

        return bucket < (uint)percentage;
    }

    private FeatureFlagPolicy PolicyFor(FeatureFlag flag) =>
        _policies.Find<FeatureFlagPolicy>(flag.PolicyName)
            ?? _policies.Find<FeatureFlagPolicy>(OperationalPolicyNames.FeatureFlag.Default)
            ?? FallbackPolicy;

    /// <summary>The deployment environment, read by the scoped surface when it builds a context.</summary>
    internal string EnvironmentName => _environment.EnvironmentName;

    private void RecordChange(FeatureFlagChange change)
    {
        _history.Enqueue(change);

        OperationsTelemetry.FlagChanges.Add(1,
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Operation, change.Flag));

        // Bounded. An unbounded audit list in memory is a leak, and the durable audit trail is
        // IAuditService — this is the operator's recent-changes view, not the record of account.
        while (_history.Count > HistoryLimit && _history.TryDequeue(out _))
        {
        }
    }
}
