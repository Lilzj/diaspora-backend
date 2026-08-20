using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Financial;

/// <summary>
/// A paid placement went live over a subject.
/// <para>
/// <b>This is the consumer B15 was published for.</b> Its event catalogue states the intent without
/// ambiguity — <c>PlacementActivatedIntegrationEvent</c> "carries a subject, a kind and a
/// <c>PriorityWeight</c>. B14 decides weighting, ranking and personalization; B15 never learns what
/// it did." The client's own Creator Studio describes a boost as buying "search priority and local
/// reach", both of which are read here rather than written there.
/// </para>
/// <para>
/// <b>The weight lands on its own field, never on <c>IsFeatured</c>.</b> B15 draws that line
/// explicitly and PSR-1.1 keeps it: a moderator's free editorial decision and a member's purchase are
/// two different facts, and one field holding both would make "did this rank because it is good or
/// because it was paid for" permanently unanswerable.
/// </para>
/// <para>
/// <b>No signal is recorded.</b> A signal is an observation about a member's interests, and the
/// purchaser buying a boost over their own listing tells this module nothing about what they want to
/// be shown. Recording one would inflate the buyer's affinity for their own category every time they
/// paid, which is a personalization profile shaped by spending rather than by reading.
/// </para>
/// </summary>
public sealed class PlacementActivatedConsumer
    : RecommendationEventConsumer<PlacementActivatedIntegrationEvent>
{
    public PlacementActivatedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Financial;

    protected override async Task HandleAsync(
        PlacementActivatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (PlacementSubjectMap.Translate(integrationEvent.SubjectType) is not { } targetType)
        {
            return;
        }

        await Projector.PromoteAsync(
            targetType,
            integrationEvent.SubjectId,
            integrationEvent.PriorityWeight,
            integrationEvent.EndsAt,
            cancellationToken);
    }
}
