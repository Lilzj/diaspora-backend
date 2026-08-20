using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Housing.Domain.Reservations;

/// <summary>How the module reaches its reservations.</summary>
public interface IHousingReservationRepository
{
    Task<HousingReservation?> GetAsync(
        HousingReservationId id, CancellationToken cancellationToken = default);

    Task<HousingReservation?> GetForReadAsync(
        HousingReservationId id, CancellationToken cancellationToken = default);

    /// <summary>The member's live hold on a property. What stops one member holding one room twice.</summary>
    Task<HousingReservation?> GetActiveForSeekerAsync(
        HousingListingId listingId,
        UserId seekerUserId,
        CancellationToken cancellationToken = default);

    Task<CursorPagedResult<HousingReservation>> QueryAsync(
        ReservationQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Live holds one member has across the whole platform. The abuse bound's input, and the most
    /// consequential count in the module: every open reservation is a home nobody else can take.
    /// </summary>
    /// <summary>How many reservations match, without loading a page of them.</summary>
    Task<int> CountAsync(
        ReservationQueryFilter filter, CancellationToken cancellationToken = default);

    Task<int> CountActiveBySeekerAsync(
        UserId seekerUserId, CancellationToken cancellationToken = default);

    /// <summary>Reservations whose hold has run out. The sweep's work list.</summary>
    Task<IReadOnlyList<HousingReservation>> GetLapsedAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(HousingReservation reservation, CancellationToken cancellationToken = default);
}
