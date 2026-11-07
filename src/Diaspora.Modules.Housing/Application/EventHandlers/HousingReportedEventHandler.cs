using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Reports;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// Publishes that a property was reported.
/// <para>
/// <b>Reads the listing back to find out whether the report actually flagged it.</b> The aggregate
/// decides that from the threshold and its current moderation state, and a consumer building a
/// safety dashboard needs to know whether this report changed anything — a report that arrived on
/// an already-flagged listing is a different signal from the one that flagged it.
/// </para>
/// <para>
/// The published contract carries no reporter identity. A member who reported a rental scam must
/// never become identifiable to the person they reported, and this is the boundary where that would
/// otherwise leak.
/// </para>
/// <para>
/// <b>Every report also goes to Trust's moderation cases (7 Nov 2026)</b>, where staff decide it.
/// That contract is the one exception to the rule above and is read by Trust alone: the reporter is
/// read from the report row here, never carried on the domain event.
/// </para>
/// </summary>
public sealed class HousingReportedEventHandler
    : DomainEventHandler<HousingReportedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;
    private readonly IHousingListingRepository _listings;
    private readonly IHousingReportRepository _reports;

    public HousingReportedEventHandler(
        HousingIntegrationEventRelay relay,
        IHousingListingRepository listings,
        IHousingReportRepository reports)
    {
        _relay = relay;
        _listings = listings;
        _reports = reports;
    }

    protected override async Task HandleAsync(
        HousingReportedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var listing = await _listings.GetForReadAsync(domainEvent.ListingId, cancellationToken);

        var wasFlagged = listing?.ModerationState
            is HousingModerationState.Flagged
            or HousingModerationState.Hidden
            or HousingModerationState.Suspended;

        await _relay.PublishAsync(domainEvent, wasFlagged, cancellationToken);

        var report = await _reports.GetAsync(domainEvent.ReportId, cancellationToken);

        if (report is not null)
        {
            await _relay.PublishReportFiledAsync(domainEvent, report, cancellationToken);
        }
    }
}
