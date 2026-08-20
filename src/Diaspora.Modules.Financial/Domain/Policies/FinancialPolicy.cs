using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Policies;

/// <summary>
/// One version of one configurable financial rule.
/// <para>
/// <b>Versions are rows, never edits.</b> Changing marketplace commission from 5% to 7% publishes
/// version 2 and closes version 1's effective window; version 1 stays in the table for ever. That is
/// not tidiness — every ledger entry stamps the version that decided its amount, and recomputing a
/// historical entry from today's policy would produce a plausible, wrong number with nothing
/// anywhere reporting a problem. The platform's handbook names that failure mode as its most
/// expensive recurring class.
/// </para>
/// <para>
/// <b>Resolution is most-specific-first.</b> A product override beats a country override, which
/// beats a module override, which beats the platform default. A policy with no applicable version
/// is a refusal rather than a guessed zero — a commission engine that silently charges nothing when
/// its configuration is missing is worse than one that stops.
/// </para>
/// <para>
/// <b>Parameters are strings.</b> The rule implementation that reads a parameter is the thing that
/// knows its type; a typed union here would mean this module knowing every rule that will ever
/// exist, which is the opposite of what a policy engine is for.
/// </para>
/// </summary>
public sealed class FinancialPolicy : AggregateRoot<FinancialPolicyId>, IAuditableEntity
{
    private readonly List<PolicyParameter> _parameters = [];

    private FinancialPolicy(
        FinancialPolicyId id,
        string policyKey,
        FinancialPolicyKind kind,
        PolicyScopeKind scope,
        string? scopeValue,
        int version,
        DateWindow effective,
        string description,
        DateTimeOffset createdAt) : base(id)
    {
        PolicyKey = policyKey;
        Kind = kind;
        Scope = scope;
        ScopeValue = scopeValue;
        Version = version;
        Effective = effective;
        Description = description;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private FinancialPolicy() { }

    /// <summary>The stable identity of the rule across versions. <c>marketplace.commission</c>.</summary>
    public string PolicyKey { get; private set; } = string.Empty;

    public FinancialPolicyKind Kind { get; private set; }

    public PolicyScopeKind Scope { get; private set; }

    /// <summary>The country code, module name or product code this version narrows to.</summary>
    public string? ScopeValue { get; private set; }

    /// <summary>1 for the first. Monotonic per key and scope.</summary>
    public int Version { get; private set; }

    /// <summary>When this version applies. Closed when a later version supersedes it.</summary>
    public DateWindow Effective { get; private set; } = default!;

    public string Description { get; private set; } = string.Empty;

    /// <summary>False once superseded or retired. A closed window is not enough on its own.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// A version the platform will not let a deployment retire. The refund-window policy and the
    /// platform-fee floor are guarantees rather than settings.
    /// </summary>
    public bool IsSystem { get; private set; }

    public Guid? PublishedByUserId { get; private set; }

    public IReadOnlyList<PolicyParameter> Parameters => _parameters.AsReadOnly();

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>What goes onto a ledger entry's audit stamp. <c>marketplace.commission@2</c>.</summary>
    public string VersionLabel => $"{PolicyKey}@{Version}";

    /// <summary>How specific this version is. Higher wins in resolution.</summary>
    public int Specificity => Scope switch
    {
        PolicyScopeKind.Product => 4,
        PolicyScopeKind.Country => 3,
        PolicyScopeKind.Module => 2,
        _ => 1
    };

    public static FinancialPolicy Publish(
        FinancialPolicyId id,
        string policyKey,
        FinancialPolicyKind kind,
        PolicyScopeKind scope,
        string? scopeValue,
        int version,
        DateWindow effective,
        string description,
        DateTimeOffset createdAt,
        bool isSystem = false,
        Guid? publishedByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(effective);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);

        if (scope != PolicyScopeKind.Platform && string.IsNullOrWhiteSpace(scopeValue))
        {
            throw new DomainException(
                $"A {scope}-scoped policy needs a scope value.",
                FinancialErrorCodes.PolicyUnresolved);
        }

        var policy = new FinancialPolicy(
            id,
            policyKey.Trim().ToLowerInvariant(),
            kind,
            scope,
            NormalizeScopeValue(scope, scopeValue),
            version,
            effective,
            description.Trim(),
            createdAt)
        {
            IsSystem = isSystem,
            PublishedByUserId = publishedByUserId
        };

        policy.Announce("published", createdAt);

        return policy;
    }

    public void SetParameter(Guid parameterId, string key, string value)
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
            _parameters.Remove(existing);
        }

        _parameters.Add(PolicyParameter.Create(parameterId, normalized, value));
        Revision++;
    }

    /// <summary>
    /// Closes this version's window because a later one took over.
    /// <para>
    /// Refuses on a system policy only when it would leave the key with no active version at all —
    /// which the caller cannot see, so the check lives on the aggregate that can.
    /// </para>
    /// </summary>
    public void Supersede(DateTimeOffset supersededAt)
    {
        if (!IsActive)
        {
            return;
        }

        Effective = Effective.EndingAt(supersededAt);
        IsActive = false;
        Revision++;
        ModifiedAt = supersededAt;

        Announce("superseded", supersededAt);
    }

    public void Retire(DateTimeOffset retiredAt)
    {
        if (IsSystem)
        {
            throw new DomainException(
                $"The '{PolicyKey}' policy protects a platform guarantee and cannot be retired. "
                + "Publish a new version instead.",
                FinancialErrorCodes.PolicyImmutable);
        }

        if (!IsActive)
        {
            return;
        }

        Effective = Effective.EndingAt(retiredAt);
        IsActive = false;
        Revision++;
        ModifiedAt = retiredAt;

        Announce("retired", retiredAt);
    }

    /// <summary>Whether this version governs a moment.</summary>
    public bool AppliesAt(DateTimeOffset moment) => IsActive && Effective.Contains(moment);

    /// <summary>Whether this version's scope matches a request.</summary>
    public bool Matches(string? countryCode, string? moduleName, string? productCode) => Scope switch
    {
        PolicyScopeKind.Platform => true,
        PolicyScopeKind.Country => Same(ScopeValue, countryCode),
        PolicyScopeKind.Module => Same(ScopeValue, moduleName),
        PolicyScopeKind.Product => Same(ScopeValue, productCode),
        _ => false
    };

    public string? Parameter(string key) =>
        _parameters
            .Find(parameter => string.Equals(
                parameter.Key, key?.Trim().ToLowerInvariant(), StringComparison.Ordinal))
            ?.Value;

    public decimal? DecimalParameter(string key) =>
        _parameters
            .Find(parameter => string.Equals(
                parameter.Key, key?.Trim().ToLowerInvariant(), StringComparison.Ordinal))
            ?.AsDecimal();

    public long? Int64Parameter(string key) =>
        _parameters
            .Find(parameter => string.Equals(
                parameter.Key, key?.Trim().ToLowerInvariant(), StringComparison.Ordinal))
            ?.AsInt64();

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

    private static bool Same(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(left, right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeScopeValue(PolicyScopeKind scope, string? scopeValue)
    {
        if (string.IsNullOrWhiteSpace(scopeValue))
        {
            return null;
        }

        var trimmed = scopeValue.Trim();

        return scope == PolicyScopeKind.Country ? trimmed.ToUpperInvariant() : trimmed.ToLowerInvariant();
    }

    private void Announce(string changeType, DateTimeOffset changedAt) =>
        RaiseDomainEvent(new FinancialPolicyChangedDomainEvent
        {
            PolicyId = Id,
            PolicyKey = PolicyKey,
            Kind = Kind,
            Scope = Scope,
            ScopeValue = ScopeValue,
            Version = Version,
            ChangeType = changeType,
            ChangedByUserId = PublishedByUserId,
            EffectiveFrom = Effective.StartsAt,
            ChangedAt = changedAt
        });
}
