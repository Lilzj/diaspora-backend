using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Reservations;

/// <summary>The caller reservations, as a member or as a landlord.</summary>
[Authorize(Permission = HousingPermissions.ReservationManage)]
public sealed record ListHousingReservationsQuery
    : IQuery<IReadOnlyList<HousingReservationResponse>>
{
    /// <summary>Seeker or Landlord. Defaults to the member view.</summary>
    public string? Side { get; init; }

    /// <summary>Only reservations still holding units.</summary>
    public bool ActiveOnly { get; init; } = true;

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
