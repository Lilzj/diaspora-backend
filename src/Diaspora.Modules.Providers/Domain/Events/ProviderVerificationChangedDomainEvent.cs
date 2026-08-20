using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised when Trust's decision about a provider changed, in either direction.
/// <para>
/// <b>The most consequential event this module publishes.</b> It moves a verified badge onto or
/// off a provider card, and a revocation that a consumer misses leaves a badge on a business
/// whose registration was withdrawn — which is worse than never having shown one.
/// </para>
/// <para>
/// <see cref="IsVerifiedNow"/> is the rolled-up answer after the change, so a consumer that only
/// renders a badge does not have to hold every claim and recompute the rollup itself. That
/// recomputation is exactly the three lines every consumer would write slightly differently.
/// </para>
/// </summary>
public sealed record ProviderVerificationChangedDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required ProviderVerificationKind Kind { get; init; }

    public required ProviderVerificationState PreviousState { get; init; }

    public required ProviderVerificationState CurrentState { get; init; }

    /// <summary>The provider's overall verified flag after this change.</summary>
    public required bool IsVerifiedNow { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public string? ReasonCode { get; init; }

    /// <summary>
    /// The provider's longest-standing owner, stamped by the aggregate — B13.1. Trust decided
    /// this and the owner is the one person it is about.
    /// </summary>
    public UserId? OwnerUserId { get; init; }
}
