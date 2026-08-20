using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Housing;

/// <summary>
/// A neighbourhood's summary was recomputed.
/// <para>
/// <b>The only route by which a neighbourhood becomes recommendable at all.</b> B4 owns the
/// geographic hierarchy and B11 owns what living somewhere is like; neither publishes a
/// "neighbourhood exists" contract, because neither needs one. This event carries enough — an
/// identifier, a name, a country — for the neighbourhoods surface to have something to rank, which
/// is why it is projected rather than merely recorded.
/// </para>
/// <para>
/// <b>The projection is deliberately thin.</b> A neighbourhood card in the client shows a name and a
/// summary; everything a member actually wants to know about a place is fetched from Housing when
/// they open it. Carrying more here would be this module accumulating a second copy of somebody
/// else's domain.
/// </para>
/// </summary>
public sealed class NeighborhoodSummaryUpdatedConsumer
    : RecommendationEventConsumer<NeighborhoodSummaryUpdatedIntegrationEvent>
{
    public NeighborhoodSummaryUpdatedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Housing;

    protected override async Task HandleAsync(
        NeighborhoodSummaryUpdatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // An unapproved summary is a draft. Projecting it would put a neighbourhood card in the feed
        // that the module which owns it has not agreed to show anybody.
        if (!integrationEvent.IsApproved)
        {
            await Projector.WithdrawAsync(
                RecommendationTargetType.Neighborhood, integrationEvent.AreaId, cancellationToken);

            return;
        }

        await Projector.ProjectAsync(
            new CandidateProjectionRequest
            {
                TargetType = RecommendationTargetType.Neighborhood,
                TargetId = integrationEvent.AreaId,
                SourceModule = SourceModule,
                Title = integrationEvent.Name,
                Slug = integrationEvent.Slug,
                CountryCode = integrationEvent.CountryCode,
                NeighborhoodName = integrationEvent.Name,
                Visibility = CandidateVisibility.Public,
                SourceUpdatedAt = integrationEvent.ChangedAt,
                PublishedAt = integrationEvent.ChangedAt
            },
            cancellationToken);
    }
}
