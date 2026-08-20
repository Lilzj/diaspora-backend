using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised on every lifecycle move.
/// <para>
/// <b>One event rather than seven, and the relay decides which published contract it becomes.</b>
/// That is what keeps the domain from learning what the wire contracts are called: the aggregate
/// reports that it moved from Available to Occupied, and the translation into
/// <c>HousingOccupied</c> lives in exactly one file.
/// </para>
/// </summary>
public sealed record HousingListingStatusChangedDomainEvent : DomainEvent
{
    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    public required HousingListingStatus PreviousStatus { get; init; }

    public required HousingListingStatus CurrentStatus { get; init; }

    public UserId? ChangedBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
