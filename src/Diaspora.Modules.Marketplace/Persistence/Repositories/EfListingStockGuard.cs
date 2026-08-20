using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Persistence.Atomic.Reservations;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Marketplace.Persistence.Repositories;

/// <summary>
/// The stock guard, on the platform's guarded atomic reservation primitive.
/// <para>
/// <b>Two gates, and each catches what the other cannot.</b> The aggregate decides first, from what
/// it loaded: whether the listing is deleted, whether it accepts commitments at all, whether the
/// quantity is sane, whether there is anything left. Those are Marketplace's rules, they produce
/// Marketplace's error codes, and they are answered without a round trip. Then the database
/// decides, with the row locked, whether the stock is <em>still</em> there. Before this phase there
/// was no second gate.
/// </para>
/// <para>
/// <b>The in-memory transition is not redundant.</b> It is what moves the listing to
/// <c>Reserved</c> when the last unit goes, adjusts the engagement counters, stamps the sale and
/// raises <c>ListingSold</c>. All of that still happens exactly as it did. What changes is that the
/// three inventory columns it computed are then replaced by the ones the database settled on and
/// marked as already persisted, so the unit of work cannot write a stale count over an arbitrated
/// one.
/// </para>
/// <para>
/// <b>It does not save,</b> so the hold and the reservation that justifies it commit together.
/// </para>
/// </summary>
public sealed class EfListingStockGuard : IListingStockGuard
{
    /// <summary>
    /// The three columns, named once. <c>Quantity − Reserved − Sold</c> is what the database
    /// re-checks on every hold, and this declaration is the whole of Marketplace's part in it.
    /// </summary>
    private static readonly AtomicCapacity<MarketplaceListing> Stock =
        AtomicCapacity<MarketplaceListing>.Of(
            listing => listing.Inventory.Quantity,
            listing => listing.Inventory.Reserved,
            listing => listing.Inventory.Sold);

    private readonly IAtomicReservationStore _reservations;
    private readonly ILogger<EfListingStockGuard> _logger;

    public EfListingStockGuard(
        IAtomicReservationStore reservations,
        ILogger<EfListingStockGuard> logger)
    {
        _reservations = reservations;
        _logger = logger;
    }

    public async Task<Result> HoldAsync(
        MarketplaceListing listing,
        int units,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);

        var held = listing.HoldStock(units, now);

        if (held.IsFailure)
        {
            return held;
        }

        // Untracked stock is counted but not rationed: the seller told the platform not to keep a
        // count, and ListingInventory.Hold obliges by incrementing Reserved and refusing nothing.
        // What actually closes such a listing is AcceptsCommitments, above, which the aggregate has
        // already applied. Imposing the capacity guard here would refuse holds the module has always
        // allowed — the arithmetic it disclaimed is not arithmetic this layer may reinstate.
        if (!listing.Inventory.IsTracked)
        {
            return Result.Success();
        }

        var outcome = await _reservations.ReserveAsync(
            new AtomicReservation<MarketplaceListing>(Stock, listing.Id, units)
            {
                Synchronize = listing
            },
            cancellationToken);

        if (outcome.Applied)
        {
            return Result.Success();
        }

        // Somebody else took the stock between the load and the statement. Put the aggregate back:
        // the caller returns a failure and saves nothing, but a guard that left a phantom hold on a
        // tracked instance would be one refactoring away from persisting it.
        listing.ReleaseStock(units, now);

        _logger.LogDebug(
            "Holding {Units} unit(s) of listing {ListingId} lost the race at the database.",
            units,
            listing.Id.Value);

        return Result.Failure(MarketplaceErrors.InsufficientStock);
    }

    public async Task<Result> ReleaseAsync(
        MarketplaceListing listing,
        int units,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);

        var released = listing.ReleaseStock(units, now);

        if (released.IsFailure || !listing.Inventory.IsTracked)
        {
            return released;
        }

        await _reservations.ReleaseAsync(
            new AtomicReservation<MarketplaceListing>(Stock, listing.Id, units)
            {
                Synchronize = listing
            },
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> SettleAsync(
        MarketplaceListing listing,
        int units,
        DateTimeOffset now,
        UserId? buyerUserId,
        ListingReservationId? reservationId,
        decimal? amount,
        string? currencyCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);

        var settled = listing.SettleStock(
            units, now, buyerUserId, reservationId, amount, currencyCode);

        if (settled.IsFailure || !listing.Inventory.IsTracked)
        {
            return settled;
        }

        // Consume rather than reserve: the units are already held, and the move between the two
        // counters is bounded by what is actually there, so a drifted pair cannot gain a sale.
        await _reservations.ConsumeAsync(
            new AtomicReservation<MarketplaceListing>(Stock, listing.Id, units)
            {
                Synchronize = listing
            },
            cancellationToken);

        return Result.Success();
    }
}
