using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A financial policy gained a version, or an existing one was superseded.
/// <para>
/// <b>A policy change is invisible and enormous.</b> Raising marketplace commission from 5% to 7%
/// does not fail, does not log an error and does not change any response shape — every sale from
/// that moment simply pays the platform more. This event and the audit entry are the only two records
/// that it happened.
/// </para>
/// </summary>
public sealed record FinancialPolicyChangedIntegrationEvent : IntegrationEvent
{
    public required Guid PolicyId { get; init; }

    public required string PolicyKey { get; init; }

    public required string Kind { get; init; }

    /// <summary><c>Platform</c>, <c>Country</c>, <c>Module</c> or <c>Product</c>.</summary>
    public required string Scope { get; init; }

    public string? ScopeValue { get; init; }

    public required int Version { get; init; }

    /// <summary><c>published</c>, <c>superseded</c> or <c>retired</c>.</summary>
    public required string ChangeType { get; init; }

    public Guid? ChangedByUserId { get; init; }

    public required DateTimeOffset EffectiveFrom { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
