using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// A seller's Trust verification changed.
/// <para>
/// <b>Published on both directions, and the revocation is the important one.</b> A consumer that
/// only heard about grants would keep a "Verified Business" badge on a seller Trust has since
/// withdrawn — the platform vouching for exactly the wrong party, which on a marketplace is the
/// most damaging thing a stale cache can do.
/// </para>
/// <para>
/// <b>Marketplace does not decide any of this.</b> Trust does; this event is the projection
/// travelling outward, and it exists so consumers do not each subscribe to Trust and re-derive
/// what a marketplace seller badge means.
/// </para>
/// </summary>
public sealed record SellerVerifiedIntegrationEvent : IntegrationEvent
{
    public required Guid SellerId { get; init; }

    public required Guid OwnerUserId { get; init; }

    public required string SellerName { get; init; }

    /// <summary><c>None</c>, <c>Pending</c>, <c>Verified</c>, <c>Rejected</c>, <c>Revoked</c>.</summary>
    public required string PreviousState { get; init; }

    public required string CurrentState { get; init; }

    public required bool IsVerified { get; init; }

    /// <summary>The seller badge as the module now computes it.</summary>
    public required string SellerType { get; init; }

    /// <summary>Trust's case identifier.</summary>
    public Guid? VerificationId { get; init; }

    /// <summary>Trust's badge label. Rendered, never interpreted.</summary>
    public string? Badge { get; init; }

    /// <summary>Why Trust refused or withdrew. Never shown to buyers.</summary>
    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
