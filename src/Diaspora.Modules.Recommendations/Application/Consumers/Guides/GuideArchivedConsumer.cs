using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Guides;

/// <summary>
/// A guide was retired.
/// <para>
/// The only way a guide stops being recommended, since nothing here sets an expiry — see
/// <see cref="GuidePublishedConsumer"/>. Archiving is usually how a factually wrong guide is
/// withdrawn, which makes this one of the few consumers whose failure would leave the platform
/// actively misinforming people.
/// </para>
/// </summary>
public sealed class GuideArchivedConsumer : RecommendationEventConsumer<GuideArchivedIntegrationEvent>
{
    public GuideArchivedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Guides;

    protected override Task HandleAsync(
        GuideArchivedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return Projector.WithdrawAsync(
            RecommendationTargetType.Guide, integrationEvent.GuideId, cancellationToken);
    }
}
