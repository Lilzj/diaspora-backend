using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Housing.Domain.Reservations;

/// <summary>
/// How a reservation list is narrowed.
/// <para>
/// Scoped to a party for the same reason a viewing list is: a reservation is private to the two
/// people in it, and there is no unscoped listing of who is about to move where.
/// </para>
/// </summary>
public sealed record ReservationQueryFilter
{
    public UserId? SeekerUserId { get; init; }

    public LandlordProfileId? LandlordId { get; init; }

    public HousingListingId? ListingId { get; init; }

    public IReadOnlyCollection<HousingReservationStatus>? Statuses { get; init; }

    /// <summary>Only reservations still holding units. What the availability question asks.</summary>
    public bool HoldingUnitsOnly { get; init; }

    public int Limit { get; init; } = HousingLimits.DefaultPageSize;

    public int EffectiveLimit => Math.Clamp(Limit, 1, HousingLimits.MaxPageSize);
}
