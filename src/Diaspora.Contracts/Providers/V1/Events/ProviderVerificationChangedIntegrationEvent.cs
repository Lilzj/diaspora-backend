using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Providers.V1.Events;

/// <summary>
/// Trust's decision about a provider changed, in either direction.
/// <para>
/// <b>The most consequential event this module publishes.</b> It moves a verified badge onto or
/// off a provider card, and a revocation a consumer misses leaves a badge on a business whose
/// registration was withdrawn — which is worse than never having shown one.
/// </para>
/// <para>
/// <see cref="IsVerifiedNow"/> is the rolled-up answer after the change, so a consumer that only
/// renders a badge does not have to hold every claim and recompute the rollup itself. That
/// recomputation is exactly the three lines every consumer would write slightly differently.
/// </para>
/// </summary>
public sealed record ProviderVerificationChangedIntegrationEvent : IntegrationEvent
{
    public required Guid ProviderId { get; init; }

    /// <summary>The <c>ProviderVerificationKind</c> name.</summary>
    public required string Kind { get; init; }

    /// <summary>The <c>ProviderVerificationState</c> name before the change.</summary>
    public required string PreviousState { get; init; }

    /// <summary>The <c>ProviderVerificationState</c> name after it.</summary>
    public required string CurrentState { get; init; }

    /// <summary>The provider's overall verified flag after this change.</summary>
    public required bool IsVerifiedNow { get; init; }

    /// <summary>When the grant lapses. Null when it does not.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>SCREAMING_SNAKE, matching Trust's own vocabulary. Null unless refused.</summary>
    public string? ReasonCode { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>
    /// The provider's notifiable member — B13.1. Its current primary owner.
    /// <para>
    /// <b>Trust decided this, not the provider.</b> Both directions are news the business learns
    /// rather than causes, and <see cref="ReasonCode"/> is written for this member and shown to
    /// nobody browsing the directory. A revocation the owner hears about from a customer is the
    /// platform failing them twice.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the provider could not be read.</para>
    /// </summary>
    public Guid? OwnerUserId { get; init; }
}
