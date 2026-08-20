using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Events;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Policies;

/// <summary>
/// A rule about what may never be recommended, as reference data.
/// <para>
/// <b>Configuration rather than code, and the reason is that these rules are argued about.</b>
/// "Should a member who has not verified see housing from another country" is a product and
/// sometimes a legal question whose answer differs by country and changes; expressing it as a
/// catalogue row means answering it is an operator action with an audit trail, not a deployment.
/// </para>
/// <para>
/// <b><see cref="IsSystem"/> marks the ones a deployment may not delete.</b> The blocked-content
/// policy and the self-authored policy are not opinions — a platform that can be configured to
/// recommend a member the seller they blocked has a defect, not a setting. System policies can be
/// reordered and parameterised; they cannot be disabled, and the aggregate refuses rather than
/// letting an API validator be the only thing standing in the way.
/// </para>
/// <para>
/// <b>Surfaces are a comma-separated list rather than an owned collection.</b> It is read whole on
/// every generation, written once, and never queried by element; a child table would be a join on
/// the hottest path in the module bought for tidiness.
/// </para>
/// </summary>
public sealed class RecommendationPolicy : AggregateRoot<RecommendationPolicyId>, IAuditableEntity
{
    private readonly List<ConfigurationParameter> _parameters = [];

    private RecommendationPolicy(
        RecommendationPolicyId id,
        string key,
        RecommendationPolicyKind kind,
        PolicyEffect effect,
        string description,
        DateTimeOffset createdAt) : base(id)
    {
        Key = key;
        Kind = kind;
        Effect = effect;
        Description = description;
        CreatedAt = createdAt;
        IsEnabled = true;
        DemotionFactor = 1m;
    }

    private RecommendationPolicy() { }

    /// <summary>The stable key an implementation is resolved by. Unique across the platform.</summary>
    public string Key { get; private set; } = string.Empty;

    public RecommendationPolicyKind Kind { get; private set; }

    public PolicyEffect Effect { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    /// <summary>Cannot be disabled or deleted. See the class summary.</summary>
    public bool IsSystem { get; private set; }

    /// <summary>Lower runs first. Exclusions before demotions, cheap checks before expensive ones.</summary>
    public int Order { get; private set; }

    /// <summary>What a matched candidate's score is multiplied by. Ignored unless the effect is a demotion.</summary>
    public decimal DemotionFactor { get; private set; }

    /// <summary>The surfaces this applies to, comma-separated. Empty means every surface.</summary>
    public string Surfaces { get; private set; } = string.Empty;

    /// <summary>The target types this applies to, comma-separated. Empty means every type.</summary>
    public string TargetTypes { get; private set; } = string.Empty;

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<ConfigurationParameter> Parameters => _parameters.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static RecommendationPolicy Define(
        RecommendationPolicyId id,
        string key,
        RecommendationPolicyKind kind,
        PolicyEffect effect,
        string description,
        DateTimeOffset createdAt,
        int order = 100,
        bool isSystem = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var policy = new RecommendationPolicy(
            id, key.Trim().ToLowerInvariant(), kind, effect, description.Trim(), createdAt)
        {
            Order = order,
            IsSystem = isSystem
        };

        policy.RaiseDomainEvent(new RecommendationPolicyChangedDomainEvent
        {
            PolicyId = id,
            PolicyKey = policy.Key,
            Kind = kind,
            Effect = effect,
            IsEnabled = true,
            ChangeType = "created",
            ChangedAt = createdAt
        });

        return policy;
    }

    public void Describe(string description, DateTimeOffset changedAt)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        Description = description.Trim();
        Touch();
        Announce("updated", changedAt);
    }

    public void SetOrder(int order, DateTimeOffset changedAt)
    {
        Order = order;
        Touch();
        Announce("updated", changedAt);
    }

    /// <summary>
    /// Sets the demotion factor.
    /// <para>
    /// Clamped to (0, 1]. Zero would be an exclusion wearing a demotion's clothes: it would remove a
    /// candidate from the page while reporting itself in the analytics as a soft nudge, which is
    /// exactly the ambiguity <see cref="PolicyEffect"/> exists to prevent.
    /// </para>
    /// </summary>
    public void SetDemotionFactor(decimal factor, DateTimeOffset changedAt)
    {
        DemotionFactor = Math.Clamp(factor, 0.01m, 1m);
        Touch();
        Announce("updated", changedAt);
    }

    public void SetScope(
        IEnumerable<RecommendationSurface>? surfaces,
        IEnumerable<RecommendationTargetType>? targetTypes,
        DateTimeOffset changedAt)
    {
        Surfaces = string.Join(',', (surfaces ?? []).Distinct().Order().Select(surface => surface.ToString()));
        TargetTypes = string.Join(',', (targetTypes ?? []).Distinct().Order().Select(type => type.ToString()));

        Touch();
        Announce("updated", changedAt);
    }

    public void SetParameter(string key, string value, Guid parameterId)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var normalized = key.Trim().ToLowerInvariant();
        var existing = _parameters.Find(parameter =>
            string.Equals(parameter.Key, normalized, StringComparison.Ordinal));

        if (existing is not null)
        {
            existing.SetValue(value);
        }
        else
        {
            _parameters.Add(ConfigurationParameter.Create(parameterId, normalized, value));
        }

        Touch();
    }

    /// <summary>
    /// Turns the policy off.
    /// <para>
    /// Refuses on a system policy rather than returning false, because a caller that could ignore
    /// the answer is a caller that will. See the class summary.
    /// </para>
    /// </summary>
    public void Disable(DateTimeOffset changedAt)
    {
        if (IsSystem)
        {
            throw new DomainException(
                $"The '{Key}' policy protects a platform guarantee and cannot be disabled. It can be "
                + "reordered and parameterised.",
                RecommendationErrorCodes.PolicyImmutable);
        }

        if (!IsEnabled)
        {
            return;
        }

        IsEnabled = false;
        Touch();
        Announce("disabled", changedAt);
    }

    public void Enable(DateTimeOffset changedAt)
    {
        if (IsEnabled)
        {
            return;
        }

        IsEnabled = true;
        Touch();
        Announce("enabled", changedAt);
    }

    /// <summary>Whether this policy applies on a surface. An empty scope means every surface.</summary>
    public bool AppliesTo(RecommendationSurface surface) =>
        Surfaces.Length == 0 ||
        Surfaces.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(surface.ToString(), StringComparer.Ordinal);

    public bool AppliesTo(RecommendationTargetType targetType) =>
        TargetTypes.Length == 0 ||
        TargetTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(targetType.ToString(), StringComparer.Ordinal);

    public string? Parameter(string key) =>
        _parameters
            .Find(parameter => string.Equals(
                parameter.Key, key.Trim().ToLowerInvariant(), StringComparison.Ordinal))
            ?.Value;

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private void Announce(string changeType, DateTimeOffset changedAt) =>
        RaiseDomainEvent(new RecommendationPolicyChangedDomainEvent
        {
            PolicyId = Id,
            PolicyKey = Key,
            Kind = Kind,
            Effect = Effect,
            IsEnabled = IsEnabled,
            ChangeType = changeType,
            ChangedAt = changedAt
        });

    private void Touch() => Revision++;
}
