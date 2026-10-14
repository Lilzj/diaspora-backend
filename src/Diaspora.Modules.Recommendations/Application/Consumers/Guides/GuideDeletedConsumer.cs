using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Guides;

/// <summary>
/// A deleted guide leaves the feed.
/// <para>
/// <b>Added 14 Oct 2026.</b> Only archiving withdrew a guide, so one published and then deleted
/// stayed on members' Home screens as a "Popular pick" that opened onto "This guide is not
/// available".
/// </para>
/// </summary>
public sealed class GuideDeletedConsumer : RecommendationEventConsumer<GuideDeletedIntegrationEvent>
{
    public GuideDeletedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Guides;

    protected override Task HandleAsync(
        GuideDeletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return Projector.WithdrawAsync(
            RecommendationTargetType.Guide, integrationEvent.GuideId, cancellationToken);
    }
}
