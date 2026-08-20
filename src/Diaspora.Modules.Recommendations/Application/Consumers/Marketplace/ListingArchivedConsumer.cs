using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Marketplace;

/// <summary>
/// A listing was filed away or removed.
/// <para>
/// Covers the paths a sale does not: a seller who changed their mind, a moderator who took something
/// down. Both must stop the listing being recommended, and a module that only subscribed to the sale
/// would keep recommending withdrawn goods indefinitely.
/// </para>
/// </summary>
public sealed class ListingArchivedConsumer
    : RecommendationEventConsumer<ListingArchivedIntegrationEvent>
{
    public ListingArchivedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Marketplace;

    protected override Task HandleAsync(
        ListingArchivedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return Projector.WithdrawAsync(
            RecommendationTargetType.MarketplaceListing,
            integrationEvent.ListingId,
            cancellationToken);
    }
}
