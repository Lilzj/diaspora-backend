using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a Trust outcome lands on a listing.
/// <para>
/// Reachable only from the handler for a Trust integration event. Nothing a seller does can raise
/// it, which is the enforcement of "Marketplace never owns Trust data" at the level the domain can
/// actually enforce it.
/// </para>
/// </summary>
public sealed record ListingVerificationChangedDomainEvent : DomainEvent
{
    public required MarketplaceListingId ListingId { get; init; }

    public required ListingVerificationState PreviousState { get; init; }

    public required ListingVerificationState CurrentState { get; init; }

    public Guid? VerificationId { get; init; }

    public string? Badge { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
