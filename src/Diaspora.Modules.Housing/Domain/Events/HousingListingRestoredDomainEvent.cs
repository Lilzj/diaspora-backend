using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised when an archived or deleted property comes back.
/// <para>
/// <see cref="WasDeleted"/> tells a consumer which of the two it was. A restore from archive is a
/// landlord resuming; a restore from deletion is a moderator or a support action, and an audit
/// trail that could not tell them apart would be missing the interesting one.
/// </para>
/// </summary>
public sealed record HousingListingRestoredDomainEvent : DomainEvent
{
    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    public required bool WasDeleted { get; init; }
}
