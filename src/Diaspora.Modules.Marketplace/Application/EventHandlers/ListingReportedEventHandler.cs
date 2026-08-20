using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Observability;
using Diaspora.Modules.Marketplace.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// Puts a reported listing into the moderation queue, and never takes it down.
/// <para>
/// <b>A single report is an allegation.</b> A module where one report hid a listing would be one
/// where any competitor can clear a category, so flagging needs a threshold — and the threshold is
/// an option rather than a constant, because how trigger-happy a deployment's queue should be is
/// genuinely a deployment decision.
/// </para>
/// <para>
/// <b>Fraud is the exception, and it flags on the first report.</b> A scam advert is the most
/// damaging content this module can carry, and the cost of a moderator glancing at a false alarm is
/// nothing beside the cost of leaving a real one up for a second reporter to find.
/// </para>
/// <para>
/// The count is denormalised onto the listing so the queue can sort by it without a correlated
/// count per row.
/// </para>
/// </summary>
public sealed class ListingReportedEventHandler : DomainEventHandler<ListingReportedDomainEvent>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly MarketplaceIntegrationEventRelay _relay;
    private readonly IOptions<MarketplaceOptions> _options;
    private readonly IClock _clock;

    public ListingReportedEventHandler(
        IMarketplaceListingRepository listings,
        MarketplaceIntegrationEventRelay relay,
        IOptions<MarketplaceOptions> options,
        IClock clock)
    {
        _listings = listings;
        _relay = relay;
        _options = options;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        ListingReportedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        MarketplaceTelemetry.ListingsReported.Add(
            1, new KeyValuePair<string, object?>("reason", domainEvent.Reason.ToString()));

        var listing = await _listings.GetAsync(domainEvent.ListingId, cancellationToken);
        var flagged = false;

        if (listing is not null && ShouldFlag(domainEvent))
        {
            listing.RecordReport(domainEvent.TotalReports);

            flagged = listing.Flag(
                flaggedBy: null,
                $"Reported {domainEvent.TotalReports} time(s). Latest reason: "
                + $"{domainEvent.Reason}.",
                _clock.UtcNow).IsSuccess;
        }
        else
        {
            listing?.RecordReport(domainEvent.TotalReports);
        }

        await _relay.PublishAsync(domainEvent, flagged, cancellationToken);
    }

    /// <summary>
    /// Whether this report should move the listing into the queue. Fraud on the first, everything
    /// else on the configured threshold.
    /// </summary>
    private bool ShouldFlag(ListingReportedDomainEvent domainEvent) =>
        domainEvent.Reason is ListingReportReason.Fraud or ListingReportReason.Counterfeit
        || domainEvent.TotalReports >= Math.Max(1, _options.Value.ReportsBeforeAutoFlag);
}
