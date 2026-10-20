using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Marketplace;

/// <summary>
/// A marketplace listing went live.
/// <para>
/// <b>The seller is recorded as the owner, and that single field is what stops the platform
/// recommending somebody their own sofa.</b> Every strategy independently concludes a member's own
/// listing is the best candidate on the platform for them — they engaged with it, it matches their
/// interests, it is in their city — and the self-authored policy is the only thing that disagrees.
/// It cannot disagree without this.
/// </para>
/// <para>
/// <b>The fulfilment options become tags.</b> "Offers delivery" is exactly the kind of fact a member
/// filters on and a strategy can match without knowing what delivery is.
/// </para>
/// <para>
/// The cover media identifier is carried as an opaque reference rather than a URL — B5's rule, and
/// this module honours it: the client resolves a media reference through Media.
/// </para>
/// </summary>
public sealed class ListingPublishedConsumer
    : RecommendationEventConsumer<ListingPublishedIntegrationEvent>
{
    public ListingPublishedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Marketplace;

    protected override async Task HandleAsync(
        ListingPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var attributes = new List<(InterestKind Kind, string Key)>();
        attributes.AddRange(Attributes(InterestKind.Tag, integrationEvent.Tags));
        attributes.Add((InterestKind.Tag, integrationEvent.ListingType));
        attributes.Add((InterestKind.Tag, integrationEvent.Condition));

        if (integrationEvent.OffersDelivery) attributes.Add((InterestKind.Tag, "delivery"));
        if (integrationEvent.OffersShipping) attributes.Add((InterestKind.Tag, "shipping"));
        if (integrationEvent.OffersPickup) attributes.Add((InterestKind.Tag, "pickup"));

        await Projector.ProjectAsync(
            new CandidateProjectionRequest
            {
                TargetType = RecommendationTargetType.MarketplaceListing,
                TargetId = integrationEvent.ListingId,
                SourceModule = SourceModule,
                Title = integrationEvent.Title,
                Summary = integrationEvent.Summary,
                Slug = integrationEvent.Slug,
                MediaReference = integrationEvent.CoverMediaId?.ToString(),
                CategoryKey = integrationEvent.CategoryKey,
                CountryCode = integrationEvent.CountryCode,
                RegionName = integrationEvent.RegionName,
                CityName = integrationEvent.CityName,

                // See the class summary. The self-authored policy's only input for this vertical, and
                // the monetization surface's "your listing". The *member* behind the seller profile:
                // `SellerId` is the profile, and a profile id compared with a user id never matches,
                // so until this read the member's user id the policy never excluded anybody's own
                // listing. Null on an event published before the field existed — unknown, which the
                // projector treats as "leave the owner as it was".
                OwnerUserId = integrationEvent.SellerUserId is { } sellerUserId
                    ? UserId.From(sellerUserId)
                    : null,
                IsVerified = integrationEvent.IsSellerVerified,
                PopularityScore = Jobs.JobPublishedConsumer.InitialPopularity,
                QualityScore = integrationEvent.IsSellerVerified ? 0.7m : 0.35m,
                Visibility = CandidateVisibility.Members,
                PublishedAt = integrationEvent.PublishedAt,
                SourceUpdatedAt = integrationEvent.PublishedAt,
                ExpiresAt = integrationEvent.ExpiresAt,
                Attributes = attributes,
                ReplaceAttributes = true
            },
            cancellationToken);
    }
}
