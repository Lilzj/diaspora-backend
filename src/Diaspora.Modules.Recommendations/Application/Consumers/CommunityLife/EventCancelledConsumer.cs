using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.CommunityLife;

/// <summary>
/// An event was called off.
/// <para>
/// The expiry handles an event that simply ended; this handles one that will not happen. Both must
/// stop it being recommended, and the difference is that a cancellation can arrive weeks before the
/// expiry would — during exactly the window when the event is at its most recommendable.
/// </para>
/// </summary>
public sealed class EventCancelledConsumer
    : RecommendationEventConsumer<EventCancelledIntegrationEvent>
{
    public EventCancelledConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.CommunityLife;

    protected override Task HandleAsync(
        EventCancelledIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return Projector.WithdrawAsync(
            RecommendationTargetType.Event, integrationEvent.CommunityEventId, cancellationToken);
    }
}
