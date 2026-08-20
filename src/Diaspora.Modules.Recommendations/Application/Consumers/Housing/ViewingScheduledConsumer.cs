using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Housing;

/// <summary>
/// A seeker arranged to view a property.
/// <para>
/// <b>Housing's equivalent of an application, and weighted the same way.</b> Arranging a viewing
/// means booking time, often travel, sometimes a day off — a commitment an order of magnitude
/// stronger than a browse, and the signal that should decide what the housing surface shows next.
/// </para>
/// </summary>
public sealed class ViewingScheduledConsumer
    : RecommendationEventConsumer<ViewingScheduledIntegrationEvent>
{
    public ViewingScheduledConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Housing;

    protected override Task HandleAsync(
        ViewingScheduledIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return RecordAsync(
            Signal(
                UserId.From(integrationEvent.SeekerUserId),
                SignalKind.Applied,
                integrationEvent.ScheduledAt,
                RecommendationTargetType.HousingListing,
                integrationEvent.ListingId),
            cancellationToken);
    }
}
