using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.Persistence.Atomic.Reservations;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Housing.Persistence.Repositories;

/// <summary>
/// The unit guard, on the platform's guarded atomic reservation primitive.
/// <para>
/// <b>This class is what B10 and B11 each closed by asking for, and it is the only class in Housing
/// that changed to get it.</b> B11 declared <see cref="IHousingUnitReservationGuard"/> precisely so
/// that the concurrency mechanism could be replaced without the aggregate, the handlers, the tests
/// or the interface moving. That is what happened: the port is untouched, and the retry-under-
/// contention behaviour it used to describe is gone.
/// </para>
/// <para>
/// <b>Two gates, and each catches what the other cannot.</b> The aggregate decides first, from what
/// it loaded: whether the listing is deleted, whether the unit count is sane, whether there is
/// anything left to give. Those are Housing's rules and they are answered without a round trip.
/// Then the database decides, with the row locked, whether the units are <em>still</em> there —
/// which is the one question a loaded aggregate cannot answer, because the answer changed after it
/// was loaded. Before this phase there was no second gate, and two members reaching for the last
/// bed in the same instant both passed the first one.
/// </para>
/// <para>
/// <b>Why the in-memory transition still happens.</b> It is not redundant: it is what synchronises
/// the listing's status, stamps <c>ModifiedAt</c> and raises whatever the transition raises. The
/// counters it computes are then overwritten by the ones the database settled on, and marked as
/// already persisted, so the unit of work's own <c>UPDATE</c> cannot write a stale count over an
/// arbitrated one. The platform does that reconciliation; this class only asks for it.
/// </para>
/// <para>
/// <b>It does not save.</b> Every method mutates the tracked aggregate inside the caller's unit of
/// work and returns it, so the reservation and the hold commit together. A guard that committed on
/// its own would create exactly the window it exists to close: units held with no reservation
/// attached to them.
/// </para>
/// </summary>
public sealed class EfHousingUnitReservationGuard : IHousingUnitReservationGuard
{
    /// <summary>
    /// The three columns, named once. <c>TotalUnits − ReservedUnits − OccupiedUnits</c> is what the
    /// database re-checks on every hold, and this declaration is the whole of Housing's part in it.
    /// </summary>
    private static readonly AtomicCapacity<HousingListing> Units =
        AtomicCapacity<HousingListing>.Of(
            listing => listing.Availability.TotalUnits,
            listing => listing.Availability.ReservedUnits,
            listing => listing.Availability.OccupiedUnits);

    private readonly IHousingListingRepository _listings;
    private readonly IAtomicReservationStore _reservations;
    private readonly ILogger<EfHousingUnitReservationGuard> _logger;

    public EfHousingUnitReservationGuard(
        IHousingListingRepository listings,
        IAtomicReservationStore reservations,
        ILogger<EfHousingUnitReservationGuard> logger)
    {
        _listings = listings;
        _reservations = reservations;
        _logger = logger;
    }

    public async Task<Result<HousingListing>> TryHoldAsync(
        HousingListingId listingId,
        int units,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var listing = await LoadAsync(listingId, cancellationToken);

        if (listing is null)
        {
            return Result.Failure<HousingListing>(HousingErrors.ListingNotFound);
        }

        // Gate one: the aggregate's own rules, against the counts it loaded.
        var settled = listing.SettleUnits(held: units, released: 0, occupied: 0, vacated: 0, now);

        if (settled.IsFailure)
        {
            _logger.LogDebug(
                "Holding {Units} unit(s) on listing {ListingId} was refused by the listing: {Error}.",
                units,
                listingId.Value,
                settled.Error.Code);

            return Result.Failure<HousingListing>(settled.Error);
        }

        // Gate two: the database, against the row as it stands right now.
        var outcome = await _reservations.ReserveAsync(
            new AtomicReservation<HousingListing>(Units, listingId, units)
            {
                Synchronize = listing
            },
            cancellationToken);

        if (outcome.Applied)
        {
            return Result.Success(listing);
        }

        // Somebody else took them between the load and the statement. Put the aggregate back where
        // it was: the caller returns a failure and saves nothing, but a guard that left a phantom
        // hold on a tracked instance would be one refactoring away from persisting it.
        listing.SettleUnits(held: 0, released: units, occupied: 0, vacated: 0, now);

        _logger.LogDebug(
            "Holding {Units} unit(s) on listing {ListingId} lost the race at the database.",
            units,
            listingId.Value);

        return Result.Failure<HousingListing>(HousingErrors.NoUnitsAvailable);
    }

    public Task<Result<HousingListing>> ReleaseAsync(
        HousingListingId listingId,
        int units,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        SettleAsync(
            listingId,
            units,
            now,
            listing => listing.SettleUnits(held: 0, released: units, occupied: 0, vacated: 0, now),
            (store, reservation, token) => store.ReleaseAsync(reservation, token),
            cancellationToken);

    public Task<Result<HousingListing>> OccupyAsync(
        HousingListingId listingId,
        int units,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        SettleAsync(
            listingId,
            units,
            now,
            listing => listing.SettleUnits(held: 0, released: 0, occupied: units, vacated: 0, now),
            (store, reservation, token) => store.ConsumeAsync(reservation, token),
            cancellationToken);

    public Task<Result<HousingListing>> VacateAsync(
        HousingListingId listingId,
        int units,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        SettleAsync(
            listingId,
            units,
            now,
            listing => listing.SettleUnits(held: 0, released: 0, occupied: 0, vacated: units, now),
            (store, reservation, token) => store.RestoreAsync(reservation, token),
            cancellationToken);

    /// <summary>
    /// The unguarded settlements. None of them can fail on contention: they only ever give units
    /// back or move them between committed states, and both the value object and the statement
    /// clamp rather than refusing. A release that could be refused would be a room held forever,
    /// which is the one outcome worse than an overbooking.
    /// </summary>
    private async Task<Result<HousingListing>> SettleAsync(
        HousingListingId listingId,
        int units,
        DateTimeOffset now,
        Func<HousingListing, Result> inMemory,
        Func<IAtomicReservationStore, AtomicReservation<HousingListing>, CancellationToken,
            Task<AtomicReservationOutcome>> statement,
        CancellationToken cancellationToken)
    {
        var listing = await LoadAsync(listingId, cancellationToken);

        if (listing is null)
        {
            return Result.Failure<HousingListing>(HousingErrors.ListingNotFound);
        }

        var settled = inMemory(listing);

        if (settled.IsFailure)
        {
            return Result.Failure<HousingListing>(settled.Error);
        }

        await statement(
            _reservations,
            new AtomicReservation<HousingListing>(Units, listingId, units)
            {
                Synchronize = listing
            },
            cancellationToken);

        return Result.Success(listing);
    }

    /// <summary>
    /// Loads the listing including soft-deleted rows.
    /// <para>
    /// Deliberate: a landlord may delete a listing while a reservation on it is still open, and
    /// the units must still be returned when that reservation is cancelled. A guard that could not
    /// see the row would leave the counts permanently wrong on a listing somebody may later
    /// restore. The statement side agrees — a guarded update runs against the table rather than
    /// through the query filter — so both halves see the same row.
    /// </para>
    /// </summary>
    private Task<HousingListing?> LoadAsync(
        HousingListingId listingId, CancellationToken cancellationToken) =>
        _listings.GetIncludingDeletedAsync(listingId, cancellationToken);
}
