using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.Reservations;

/// <summary>
/// The seam between deciding that a unit can be held and making that decision durable under
/// contention.
/// <para>
/// <b>This interface was declared against a platform primitive that did not exist yet, and the
/// primitive has since landed without the interface changing — which was the point of declaring
/// it.</b> B10 closed by recommending a guarded atomic counter in <c>Diaspora.Persistence</c>; the
/// B11 brief was explicit that Housing must not invent a module-local substitute, so the
/// reservation workflow was made to depend on this abstraction and the concurrency mechanism behind
/// it was left as somebody else's decision to change. B1.3 changed it. One class moved, and
/// nothing above this line did.
/// </para>
/// <para>
/// <b>What the shipped implementation does now.</b> <c>EfHousingUnitReservationGuard</c> asks the
/// aggregate to hold the units — which answers Housing's own rules without a round trip — and then
/// issues <c>UPDATE … SET reserved_units = reserved_units + n WHERE total_units − reserved_units −
/// occupied_units &gt;= n</c> through the platform's reservation store. The database evaluates the
/// guard against the row as it stands, with it locked. Two members reaching for the last bed in the
/// same instant both pass the aggregate's check and exactly one passes the database's; the loser
/// receives <c>HousingErrors.NoUnitsAvailable</c>, and the property cannot be overbooked even by
/// two transactions neither of which is stale in any way EF could see.
/// </para>
/// <para>
/// <b>What it no longer costs.</b> The previous implementation resolved the race through optimistic
/// concurrency and a retry, which is a wait under contention and, where two holds touched no common
/// column, was not a guarantee at all. Neither is true now: the statement is arbitrated once, and
/// a buyer taking a unit deliberately does not disturb a landlord's in-flight edit.
/// </para>
/// <para>
/// <b>Why the port takes identifiers rather than an aggregate.</b> A guarded update is a statement
/// about a row, and handing it a loaded aggregate would tempt an implementation into writing
/// through the change tracker — which is exactly the path that cannot be made atomic. The guard
/// still loads the listing, because the caller needs it back; it just never decides anything from
/// what it loaded that the database is not asked to confirm.
/// </para>
/// </summary>
public interface IHousingUnitReservationGuard
{
    /// <summary>
    /// Takes units off a listing, or fails because somebody else got them first.
    /// <para>
    /// Called inside the caller's unit of work and does not commit. Returns the listing with the
    /// hold applied so the caller can raise its events and save once.
    /// </para>
    /// </summary>
    /// <returns>
    /// The listing with the units held, or <c>HousingErrors.NoUnitsAvailable</c> when they are
    /// gone. Never throws on contention — losing a race is an ordinary outcome, not a fault.
    /// </returns>
    Task<Result<HousingListing>> TryHoldAsync(
        HousingListingId listingId,
        int units,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns units to the market.
    /// <para>
    /// Unguarded and never failing, deliberately: a release that could be refused is a room held
    /// forever, which is the one outcome worse than an overbooking. The value object clamps at
    /// zero rather than throwing for the same reason.
    /// </para>
    /// </summary>
    Task<Result<HousingListing>> ReleaseAsync(
        HousingListingId listingId,
        int units,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>Converts held units into occupied ones. Somebody moved in.</summary>
    Task<Result<HousingListing>> OccupyAsync(
        HousingListingId listingId,
        int units,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>A tenancy ended and the unit is back on the market.</summary>
    Task<Result<HousingListing>> VacateAsync(
        HousingListingId listingId,
        int units,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
