using Diaspora.Contracts.Knowledge.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Knowledge;

/// <summary>
/// An editor took a guide off a country's directory.
/// <para>
/// <b>Clears the flag rather than withdrawing the candidate.</b> Unfeaturing is an editorial
/// decision about prominence, not about correctness — the guide is still a guide and should still be
/// recommendable to somebody it fits. Withdrawing here would conflate "no longer the first thing we
/// show" with "no longer worth reading".
/// </para>
/// </summary>
public sealed class KnowledgeGuideUnfeaturedConsumer
    : RecommendationEventConsumer<KnowledgeGuideUnfeaturedIntegrationEvent>
{
    public KnowledgeGuideUnfeaturedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Knowledge;

    protected override async Task HandleAsync(
        KnowledgeGuideUnfeaturedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await Projector.ProjectAsync(
            new CandidateProjectionRequest
            {
                TargetType = RecommendationTargetType.Guide,
                TargetId = integrationEvent.GuideId,
                SourceModule = SourceModule,

                // The flag, not the candidate. See the class summary.
                IsFeatured = false,
                SourceUpdatedAt = integrationEvent.OccurredAt
            },
            cancellationToken);
    }
}
