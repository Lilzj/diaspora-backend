using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Marketplace;

/// <summary>A deleted marketplace listing leaves the feed (14 Oct 2026; only archive and sold did).</summary>
public sealed class ListingDeletedConsumer
    : RecommendationEventConsumer<ListingDeletedIntegrationEvent>
{
    public ListingDeletedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Marketplace;

    protected override Task HandleAsync(
        ListingDeletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return Projector.WithdrawAsync(
            RecommendationTargetType.MarketplaceListing,
            integrationEvent.ListingId,
            cancellationToken);
    }
}
