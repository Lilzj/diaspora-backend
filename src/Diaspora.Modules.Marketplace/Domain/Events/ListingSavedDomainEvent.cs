using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a member saves or unsaves a listing.
/// <para>
/// One event for both directions. The handler applies the delta to the listing's save count, and
/// deliberately does not bump its revision — a stranger saving a listing must not make the
/// seller's in-flight edit fail.
/// </para>
/// <para>
/// <b>Deliberately not published as an integration event.</b> Who saved what is the most
/// behaviourally revealing thing this module knows about a member, it is of no use to any other
/// module today, and putting it on the bus would be a privacy cost paid for nothing.
/// </para>
/// </summary>
public sealed record ListingSavedDomainEvent : DomainEvent
{
    public required MarketplaceListingId ListingId { get; init; }

    public required UserId UserId { get; init; }

    /// <summary>True on a save, false on an unsave.</summary>
    public required bool IsSaved { get; init; }
}
