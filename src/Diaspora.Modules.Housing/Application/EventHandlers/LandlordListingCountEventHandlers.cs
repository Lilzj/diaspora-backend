using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Events;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// Every lifecycle move a property makes, arriving at the landlord's counts.
/// <para>
/// <b>Publication was the only one that did.</b> <c>RecordListingCounts</c> had exactly one caller
/// — the publish handler — so archiving, letting, withdrawing or deleting a property left the
/// landlord's figures at whatever they were when they last published something.
/// </para>
/// <para>
/// <b><c>HousingListingStatusChangedDomainEvent</c> had no consumer at all.</b> It is raised by
/// <c>MoveTo</c>, which every lifecycle move goes through, and its own summary describes a relay
/// deciding "which published contract it becomes" — a relay that was never written, because the
/// specific events each got a handler of their own instead. It was a complete account of every
/// move a property makes, delivered to nobody. This is its first reader.
/// </para>
/// </summary>
public sealed class HousingListingStatusChangedCountEventHandler
    : DomainEventHandler<HousingListingStatusChangedDomainEvent>
{
    private readonly LandlordListingCountRefresher _counts;

    public HousingListingStatusChangedCountEventHandler(LandlordListingCountRefresher counts) =>
        _counts = counts;

    protected override Task HandleAsync(
        HousingListingStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _counts.RefreshAsync(domainEvent.LandlordId, cancellationToken);
}

/// <summary>
/// A property was soft-deleted.
/// <para>
/// A separate handler because a soft delete moves no status: <c>MarkDeleted</c> sets
/// <c>IsDeleted</c> and raises this, so the status-changed handler above never hears about it —
/// and a deleted property is the one case where the count most obviously must fall.
/// </para>
/// </summary>
public sealed class HousingListingDeletedCountEventHandler
    : DomainEventHandler<HousingListingDeletedDomainEvent>
{
    private readonly LandlordListingCountRefresher _counts;

    public HousingListingDeletedCountEventHandler(LandlordListingCountRefresher counts) =>
        _counts = counts;

    protected override Task HandleAsync(
        HousingListingDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _counts.RefreshAsync(domainEvent.LandlordId, cancellationToken);
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
    private readonly LandlordListingCountRefresher _counts;

    public HousingListingRestoredCountEventHandler(LandlordListingCountRefresher counts) =>
        _counts = counts;

    protected override Task HandleAsync(
        HousingListingRestoredDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _counts.RefreshAsync(domainEvent.LandlordId, cancellationToken);
}
