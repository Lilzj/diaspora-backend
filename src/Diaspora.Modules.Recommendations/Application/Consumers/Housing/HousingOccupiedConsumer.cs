using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Housing;

/// <summary>
/// A property was let.
/// <para>
/// <b>Withdrawn only when fully occupied, for the same reason a partly-sold listing stays.</b> A
/// building with twelve units that let one still has eleven, and a module that withdrew on the first
/// tenancy would empty the housing surface of exactly the properties that are working.
/// </para>
/// <para>
/// <b>The occupant's signal is a transaction, and it is also a location.</b> Somebody who just took a
/// flat in a neighbourhood is now, with near certainty, living in it — a stronger location signal
/// than anything the registration form collected, and the reason the context is carried rather than
/// left for the next login to supply.
/// </para>
/// </summary>
public sealed class HousingOccupiedConsumer
    : RecommendationEventConsumer<HousingOccupiedIntegrationEvent>
{
    public HousingOccupiedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Housing;

    protected override async Task HandleAsync(
        HousingOccupiedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.OccupantUserId is { } occupantId)
        {
            var occupant = UserId.From(occupantId);
            var context = SignalContext.Create(integrationEvent.CountryCode);

            await RecordAsync(
                new List<SignalDraft>
                {
                    Signal(
                        occupant,
                        SignalKind.Transacted,
                        integrationEvent.OccupiedAt,
                        RecommendationTargetType.HousingListing,
                        integrationEvent.ListingId,
                        integrationEvent.AccommodationTypeKey,
                        InterestKind.Category,
                        context),

                    // They live there now. See the class summary.
                    Signal(occupant, SignalKind.Location, integrationEvent.OccupiedAt, context: context)
                },
                cancellationToken);
        }

        if (integrationEvent.IsFullyOccupied)
        {
            await Projector.WithdrawAsync(
                RecommendationTargetType.HousingListing, integrationEvent.ListingId, cancellationToken);
        }
    }
}
