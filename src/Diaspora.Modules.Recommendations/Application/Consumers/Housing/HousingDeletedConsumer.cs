using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Housing;

/// <summary>A deleted home leaves the feed (14 Oct 2026; only a let home did).</summary>
public sealed class HousingDeletedConsumer : RecommendationEventConsumer<HousingDeletedIntegrationEvent>
{
    public HousingDeletedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Housing;

    protected override Task HandleAsync(
        HousingDeletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return Projector.WithdrawAsync(
            RecommendationTargetType.HousingListing, integrationEvent.ListingId, cancellationToken);
    }
}
