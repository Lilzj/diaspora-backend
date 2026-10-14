using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Housing;

/// <summary>
/// A neighbourhood's summary was recomputed — and since 14 Oct 2026, nothing is projected from it.
/// <para>
/// <b>Neighbourhood cards are dropped</b> (owner decision, Phase 2B of retiring the place
/// catalogue). This consumer was the only route by which a neighbourhood became a recommendation
/// candidate, keyed by the catalogue node's <c>AreaId</c>. With the catalogue retiring, a
/// neighbourhood page becomes "the area around a point", which has no identity to recommend, so
/// the consumer no longer projects or withdraws anything. Candidates projected before then are
/// left as they are: inert rows nothing refreshes.
/// </para>
/// <para>
/// <b>Still registered rather than deleted</b>, so the event keeps a consumer and the platform's
/// "every published event is consumed or deliberately ignored" checks keep their answer; deleting
/// it is Phase 3's, with the catalogue.
/// </para>
/// </summary>
public sealed class NeighborhoodSummaryUpdatedConsumer
    : RecommendationEventConsumer<NeighborhoodSummaryUpdatedIntegrationEvent>
{
    public NeighborhoodSummaryUpdatedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Housing;

    protected override Task HandleAsync(
        NeighborhoodSummaryUpdatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Deliberately nothing. See the class summary.
        return Task.CompletedTask;
    }
}
