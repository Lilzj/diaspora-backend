using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Housing;

/// <summary>
/// A property listing went live.
/// <para>
/// <b>The one vertical where the neighbourhood is projected, and it is the most valuable location
/// field the platform has.</b> A member looking for a flat cares about the neighbourhood far more
/// than the city; the housing surface weights location heavily precisely because this consumer
/// carries the fine grain.
/// </para>
/// <para>
/// <b>The suitability flags become tags rather than columns.</b> Student-friendly, family-friendly,
/// shared — these are what a member actually filters on, and as tags they meet the profile's
/// interests in the shared vocabulary rather than needing three boolean columns and three
/// special-case comparisons in the interest strategy.
/// </para>
/// <para>
/// <b>Quality leans harder on verification here than anywhere else.</b> A member is about to send a
/// deposit to a stranger in a country they have just arrived in, and an unverified landlord is not a
/// slightly worse listing — it is a different kind of risk.
/// </para>
/// </summary>
public sealed class HousingPublishedConsumer
    : RecommendationEventConsumer<HousingPublishedIntegrationEvent>
{
    public HousingPublishedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Housing;

    protected override async Task HandleAsync(
        HousingPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var attributes = new List<(InterestKind Kind, string Key)>();
        attributes.AddRange(Attributes(InterestKind.Tag, integrationEvent.AmenityKeys));
        attributes.Add((InterestKind.Tag, integrationEvent.FurnishedStatus));
        attributes.Add((InterestKind.Tag, $"bedrooms-{integrationEvent.Bedrooms}"));

        // What members actually filter on. See the class summary.
        if (integrationEvent.IsStudentFriendly) attributes.Add((InterestKind.Tag, "student-friendly"));
        if (integrationEvent.IsFamilyFriendly) attributes.Add((InterestKind.Tag, "family-friendly"));
        if (integrationEvent.IsSharedAccommodation) attributes.Add((InterestKind.Tag, "shared"));
        if (integrationEvent.AcceptsShortStay) attributes.Add((InterestKind.Tag, "short-stay"));
        if (integrationEvent.AcceptsLongStay) attributes.Add((InterestKind.Tag, "long-stay"));

        await Projector.ProjectAsync(
            new CandidateProjectionRequest
            {
                TargetType = RecommendationTargetType.HousingListing,
                TargetId = integrationEvent.ListingId,
                SourceModule = SourceModule,
                Title = integrationEvent.Title,
                Summary = integrationEvent.Summary,
                Slug = integrationEvent.Slug,
                MediaReference = integrationEvent.CoverMediaId?.ToString(),
                CategoryKey = integrationEvent.AccommodationTypeKey,
                CountryCode = integrationEvent.CountryCode,
                RegionName = integrationEvent.RegionName,
                CityName = integrationEvent.CityName,

                // The finest location grain any vertical publishes. See the class summary.
                NeighborhoodName = integrationEvent.NeighborhoodName,
                OwnerUserId = UserId.From(integrationEvent.LandlordId),
                IsVerified = integrationEvent.IsLandlordVerified,
                PopularityScore = Jobs.JobPublishedConsumer.InitialPopularity,
                QualityScore = integrationEvent.IsLandlordVerified ? 0.8m : 0.3m,
                Visibility = CandidateVisibility.Members,
                PublishedAt = integrationEvent.PublishedAt,
                SourceUpdatedAt = integrationEvent.PublishedAt,
                Attributes = attributes,
                ReplaceAttributes = true
            },
            cancellationToken);
    }
}
