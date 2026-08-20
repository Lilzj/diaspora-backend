using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised on every move through the reservation workflow.
/// <para>
/// <b>The listing's unit counts are settled from this event and from nowhere else.</b> That single
/// path is what keeps the counts honest: a cancellation, a refusal and an expiry all return units
/// through one handler, and an occupation converts them through the same one. Settling at each
/// call site instead would mean settling correctly at most of them.
/// </para>
/// </summary>
public sealed record HousingReservationStatusChangedDomainEvent : DomainEvent
{
    public required HousingReservationId ReservationId { get; init; }

    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    public required UserId SeekerUserId { get; init; }

    public required HousingReservationStatus PreviousStatus { get; init; }

    public required HousingReservationStatus CurrentStatus { get; init; }

    public required int Units { get; init; }

    public UserId? ChangedBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
