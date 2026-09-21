using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Listings;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// Every lifecycle move a property makes, arriving at the counts that describe it.
/// <para>
/// <b>Publication was the only one that did.</b> <c>RecordListingCounts</c> had exactly one caller
/// — the publish handler — so archiving, letting, withdrawing or deleting a property left the
/// landlord's figures at whatever they were when they last published something. The
/// neighbourhood's count was refreshed in the same place and when a property was let, and nowhere
/// else, which left a directory advertising homes that had been taken off the market.
/// </para>
/// <para>
/// <b><c>HousingListingStatusChangedDomainEvent</c> had no consumer at all.</b> It is raised by
/// <c>MoveTo</c>, which every lifecycle move goes through, and its own summary describes a relay
/// deciding "which published contract it becomes" — a relay that was never written, because the
/// specific events each got a handler of their own instead. It was a complete account of every
/// move a property makes, delivered to nobody.
/// </para>
/// </summary>
public sealed class HousingListingStatusChangedCountEventHandler
    : DomainEventHandler<HousingListingStatusChangedDomainEvent>
{
    private readonly LandlordListingCountRefresher _landlordCounts;
    private readonly NeighborhoodListingCountRefresher _neighborhoodCounts;

    public HousingListingStatusChangedCountEventHandler(
        LandlordListingCountRefresher landlordCounts,
        NeighborhoodListingCountRefresher neighborhoodCounts)
    {
        _landlordCounts = landlordCounts;
        _neighborhoodCounts = neighborhoodCounts;
    }

    protected override async Task HandleAsync(
        HousingListingStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _landlordCounts.RefreshAsync(domainEvent.LandlordId, cancellationToken);
        await _neighborhoodCounts.RefreshAsync(domainEvent.ListingId, cancellationToken);
    }
}

/// <summary>
/// A property was soft-deleted.
/// <para>
/// A separate handler because a soft delete moves no status: <c>MarkDeleted</c> sets
/// <c>IsDeleted</c> and raises this, so the status-changed handler above never hears about it —
/// and a deleted property is the one case where both counts most obviously must fall.
/// </para>
/// </summary>
public sealed class HousingListingDeletedCountEventHandler
    : DomainEventHandler<HousingListingDeletedDomainEvent>
{
    private readonly LandlordListingCountRefresher _landlordCounts;
    private readonly NeighborhoodListingCountRefresher _neighborhoodCounts;

    public HousingListingDeletedCountEventHandler(
        LandlordListingCountRefresher landlordCounts,
        NeighborhoodListingCountRefresher neighborhoodCounts)
    {
        _landlordCounts = landlordCounts;
        _neighborhoodCounts = neighborhoodCounts;
    }

    protected override async Task HandleAsync(
        HousingListingDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _landlordCounts.RefreshAsync(domainEvent.LandlordId, cancellationToken);
        await _neighborhoodCounts.RefreshAsync(domainEvent.ListingId, cancellationToken);
    }
}

/// <summary>
/// A property came back.
/// <para>
/// Raised in both directions: a restore from deletion moves no status and is only heard here, and a
/// restore from archive also moves to Draft and is heard twice. Recomputing is idempotent, so being
/// told twice costs a count and changes nothing — which is the property that makes recomputation
/// the right shape for an at-least-once dispatcher.
/// </para>
/// </summary>
public sealed class HousingListingRestoredCountEventHandler
    : DomainEventHandler<HousingListingRestoredDomainEvent>
{
    private readonly LandlordListingCountRefresher _landlordCounts;
    private readonly NeighborhoodListingCountRefresher _neighborhoodCounts;

    public HousingListingRestoredCountEventHandler(
        LandlordListingCountRefresher landlordCounts,
        NeighborhoodListingCountRefresher neighborhoodCounts)
    {
        _landlordCounts = landlordCounts;
        _neighborhoodCounts = neighborhoodCounts;
    }

    protected override async Task HandleAsync(
        HousingListingRestoredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _landlordCounts.RefreshAsync(domainEvent.LandlordId, cancellationToken);
        await _neighborhoodCounts.RefreshAsync(domainEvent.ListingId, cancellationToken);
    }
}

/// <summary>
/// A moderator hid a property, or let it back.
/// <para>
/// <b>The second event in this module with no consumer.</b> Moderation is one of the three
/// conditions a neighbourhood's count turns on — a property must be public, clear of moderation and
/// on the market — so hiding one left the directory advertising a home nobody could open.
/// </para>
/// <para>
/// The landlord's own figure is deliberately not touched here. It counts what they have on the
/// market, and a moderation hold is the platform's decision about who may see the property rather
/// than a change in what the landlord is offering.
/// </para>
/// </summary>
public sealed class HousingModerationChangedCountEventHandler
    : DomainEventHandler<HousingModerationChangedDomainEvent>
{
    private readonly NeighborhoodListingCountRefresher _neighborhoodCounts;

    public HousingModerationChangedCountEventHandler(
        NeighborhoodListingCountRefresher neighborhoodCounts) =>
        _neighborhoodCounts = neighborhoodCounts;

    protected override Task HandleAsync(
        HousingModerationChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _neighborhoodCounts.RefreshAsync(domainEvent.ListingId, cancellationToken);
}

/// <summary>
/// A landlord made a property private, or public again.
/// <para>
/// The third condition the count turns on, and the quietest: <c>ChangeVisibility</c> moves no
/// status and involves no moderator, so neither handler above hears it. It arrives as an ordinary
/// edit, which is why this reads <c>ChangedFields</c> rather than recounting after every change of
/// rent or wording.
/// </para>
/// </summary>
public sealed class HousingListingVisibilityCountEventHandler
    : DomainEventHandler<HousingListingUpdatedDomainEvent>
{
    private readonly NeighborhoodListingCountRefresher _neighborhoodCounts;

    public HousingListingVisibilityCountEventHandler(
        NeighborhoodListingCountRefresher neighborhoodCounts) =>
        _neighborhoodCounts = neighborhoodCounts;

    protected override Task HandleAsync(
        HousingListingUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        domainEvent.ChangedFields.Contains(
            nameof(HousingListing.Visibility), StringComparer.Ordinal)
            ? _neighborhoodCounts.RefreshAsync(domainEvent.ListingId, cancellationToken)
            : Task.CompletedTask;
}
