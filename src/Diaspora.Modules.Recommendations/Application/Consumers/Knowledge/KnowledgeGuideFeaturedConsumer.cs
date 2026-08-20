using Diaspora.Contracts.Knowledge.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Knowledge;

/// <summary>
/// An editor put a guide on a country's directory.
/// <para>
/// <b>The one editorial signal the platform has, and it deserves to be treated as one.</b> Every
/// other input to ranking is measured — views, verifications, recency. This is a person deciding
/// that a particular guide is what somebody arriving in a particular country should read first, and
/// it is exactly the judgement that no amount of engagement data reproduces.
/// </para>
/// <para>
/// <b>It sets the featured flag rather than the popularity score.</b> Featured is a fact somebody
/// asserted; popularity is a measurement. Folding an editorial decision into a measured field would
/// make the measurement dishonest and would make the decision impossible to undo without also
/// destroying the measurement.
/// </para>
/// <para>
/// <b>The position is not carried, and that is a deliberate boundary.</b> Where a guide sits on the
/// country directory is the Knowledge Directory's own ordering for its own page; a recommendation
/// feed that mirrored it would be reproducing somebody else's editorial layout in a surface with
/// different rules.
/// </para>
/// </summary>
public sealed class KnowledgeGuideFeaturedConsumer
    : RecommendationEventConsumer<KnowledgeGuideFeaturedIntegrationEvent>
{
    public KnowledgeGuideFeaturedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Knowledge;

    protected override async Task HandleAsync(
        KnowledgeGuideFeaturedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await Projector.ProjectAsync(
            new CandidateProjectionRequest
            {
                TargetType = RecommendationTargetType.Guide,
                TargetId = integrationEvent.GuideId,

                // Attributed to Knowledge rather than Guides: the fact being projected is the
                // Directory's editorial decision, not the guide's publication.
                SourceModule = SourceModule,
                CountryCode = integrationEvent.CountryCode,

                // A fact somebody asserted, not a measurement. See the class summary.
                IsFeatured = true,
                SourceUpdatedAt = integrationEvent.ChangedAt
            },
            cancellationToken);
    }
}
