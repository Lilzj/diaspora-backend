using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.Listings;

/// <summary>
/// The seam between deciding that stock may be taken and making that decision durable under
/// contention.
/// <para>
/// <b>B10 shipped without this, and its readiness report said so plainly.</b> The handlers called
/// <c>MarketplaceListing.HoldStock</c> directly, which is correct against what the aggregate
/// loaded and says nothing about what the row holds by the time the save lands. Two buyers reaching
/// for the last phone both read <c>Available == 1</c>, both decided they could proceed, and neither
/// was stale in any way the concurrency token could see — stock movements deliberately do not bump
/// <c>Revision</c>, so a buyer cannot fail a seller's in-flight edit. Both committed. That is the
/// window B1.3 closes, and this port is where it closes.
/// </para>
/// <para>
/// <b>Why this one takes the aggregate where Housing's takes an identifier.</b> Housing declared
/// its port before the primitive existed and had to keep every implementation option open, so it
/// takes identifiers and loads. Marketplace's handlers already hold the tracked listing — they
/// loaded it to authorise the request — and taking it back off them would mean a second query for
/// a row already in the change tracker. The implementation still never decides anything from the
/// aggregate that the database is not asked to confirm; the aggregate answers Marketplace's rules,
/// and the statement answers the arithmetic.
/// </para>
/// <para>
/// <b>Only <see cref="HoldAsync"/> can lose a race.</b> Releasing and settling move units that are
/// already spoken for, and a release that could be refused would be stock nobody can ever buy.
/// </para>
/// </summary>
public interface IListingStockGuard
{
    /// <summary>
    /// Takes units off a listing, or fails because somebody else got them first.
    /// <para>
    /// Called inside the caller's unit of work and does not commit, so the hold and the reservation
    /// that justifies it are one transaction.
    /// </para>
    /// </summary>
    /// <returns>
    /// Whatever the aggregate refused it for, or <c>MarketplaceErrors.InsufficientStock</c> when the
    /// units were there at load time and gone by the time the statement ran. Never throws on
    /// contention — losing a race is an ordinary outcome, not a fault.
    /// </returns>
    Task<Result> HoldAsync(
        MarketplaceListing listing,
        int units,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>Returns held units to the shelf. Cannot be refused on contention.</summary>
    Task<Result> ReleaseAsync(
        MarketplaceListing listing,
        int units,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts held units into sold ones — the completion path of a reservation, and the only
    /// route from reserved to sold.
    /// </summary>
    Task<Result> SettleAsync(
        MarketplaceListing listing,
        int units,
        DateTimeOffset now,
        UserId? buyerUserId,
        ListingReservationId? reservationId,
        decimal? amount,
        string? currencyCode,
        CancellationToken cancellationToken = default);
}
