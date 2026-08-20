using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Financial;

/// <summary>
/// A paid placement finished.
/// <para>
/// <b>Lapsed, cancelled and refunded all end it, and this consumer treats them identically.</b> B15
/// keeps the three apart because they mean different things to a ledger; to a ranking they mean the
/// one thing that matters — nobody is paying for this position any more.
/// </para>
/// <para>
/// <b>Ending is belt and braces rather than the only defence.</b> The candidate carries the window
/// the placement was sold for, and ranking will stop honouring an expired promotion on its own even
/// if this event never arrives. That matters because B15's lifecycle sweep runs every ten minutes, so
/// without the window a lapsed boost would keep outranking organic results for up to ten minutes
/// after the member stopped paying for it.
/// </para>
/// </summary>
public sealed class PlacementEndedConsumer
    : RecommendationEventConsumer<PlacementEndedIntegrationEvent>
{
    public PlacementEndedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Financial;

    protected override async Task HandleAsync(
        PlacementEndedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (PlacementSubjectMap.Translate(integrationEvent.SubjectType) is not { } targetType)
        {
            return;
        }

        await Projector.EndPromotionAsync(
            targetType, integrationEvent.SubjectId, cancellationToken);
    }
}
