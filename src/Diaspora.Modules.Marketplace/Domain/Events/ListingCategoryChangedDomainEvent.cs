using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a listing moves between categories.
/// <para>
/// Separate from the general update event because two consumers key on it specifically: the
/// category counters on both sides need adjusting, and the automatic guide suggestions beside the
/// listing are chosen from its category and country. Both would have to parse a changed-fields
/// list otherwise, which is a contract nobody meant to freeze.
/// </para>
/// </summary>
public sealed record ListingCategoryChangedDomainEvent : DomainEvent
{
    public required MarketplaceListingId ListingId { get; init; }

    public required string PreviousCategoryKey { get; init; }

    public required string CurrentCategoryKey { get; init; }

    /// <summary>Whether the listing is publicly visible. Counters only track ones that are.</summary>
    public required bool IsDiscoverable { get; init; }
}
