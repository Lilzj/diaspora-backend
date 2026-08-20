using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>
/// A financial policy gained a version, or an existing version was superseded.
/// <para>
/// <b>A policy change is invisible and enormous.</b> Raising marketplace commission from 5% to 7%
/// does not fail, does not log an error and does not change any response shape — every sale from
/// that moment simply pays the platform more. The audit entry and this event are the only two
/// records that it happened.
/// </para>
/// </summary>
public sealed record FinancialPolicyChangedDomainEvent : DomainEvent
{
    public required FinancialPolicyId PolicyId { get; init; }

    public required string PolicyKey { get; init; }

    public required FinancialPolicyKind Kind { get; init; }

    public required PolicyScopeKind Scope { get; init; }

    public string? ScopeValue { get; init; }

    public required int Version { get; init; }

    /// <summary><c>published</c>, <c>superseded</c> or <c>retired</c>.</summary>
    public required string ChangeType { get; init; }

    public Guid? ChangedByUserId { get; init; }

    public required DateTimeOffset EffectiveFrom { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
