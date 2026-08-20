using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Guides;

/// <summary>
/// A guide was published.
/// <para>
/// <b>The most-recommended kind of thing on the platform, and the one with no expiry.</b> A guide
/// about renewing a residence permit stays useful for years, which is why the guides surface weights
/// recency at almost nothing and why nothing here sets an expiry date. The retention sweep does not
/// touch it; only an archival does.
/// </para>
/// <para>
/// <b>Public visibility, unlike every other vertical.</b> Guides are how a country's diaspora
/// explains itself, and they are the one thing this platform genuinely wants a signed-out visitor to
/// see. Everything else is projected as members-only.
/// </para>
/// <para>
/// <b>The language is carried and it matters here more than anywhere else.</b> A guide in French is
/// no use to somebody who reads only English, and unlike a flat or a job there is no photograph or
/// price to fall back on — the whole artefact is text.
/// </para>
/// </summary>
public sealed class GuidePublishedConsumer
    : RecommendationEventConsumer<GuidePublishedIntegrationEvent>
{
    public GuidePublishedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Guides;

    protected override async Task HandleAsync(
        GuidePublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await Projector.ProjectAsync(
            new CandidateProjectionRequest
            {
                TargetType = RecommendationTargetType.Guide,
                TargetId = integrationEvent.GuideId,
                SourceModule = SourceModule,
                Title = integrationEvent.Title,
                Summary = integrationEvent.Summary,
                Slug = integrationEvent.Slug,
                CategoryKey = integrationEvent.CategoryKey,
                CountryCode = integrationEvent.CountryCode,
                RegionName = integrationEvent.RegionName,
                CityName = integrationEvent.CityName,

                // See the class summary. The whole artefact is text.
                LanguageCode = integrationEvent.Language,
                OwnerUserId = UserId.From(integrationEvent.AuthorId),

                // Guides start higher than other verticals because a guide with no engagement is
                // still the answer to somebody's question, whereas a listing with no engagement is
                // just a listing nobody wanted.
                PopularityScore = 0.4m,
                QualityScore = 0.5m,
                Visibility = CandidateVisibility.Public,
                PublishedAt = integrationEvent.PublishedAt,
                SourceUpdatedAt = integrationEvent.PublishedAt,
                Attributes = Attributes(InterestKind.Tag, integrationEvent.Tags),
                ReplaceAttributes = true
            },
            cancellationToken);
    }
}
