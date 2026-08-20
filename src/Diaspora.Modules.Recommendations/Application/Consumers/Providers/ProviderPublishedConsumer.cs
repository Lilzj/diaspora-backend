using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Providers;

/// <summary>
/// A service provider became visible.
/// <para>
/// <b>The thinnest payload of any publication contract, and it constrains what this surface can
/// do.</b> Providers publishes a name, a type, a category identifier and a country — no summary, no
/// tags, no rating. The providers surface therefore ranks almost entirely on trust, location and
/// popularity, which is defensible for a service directory and is a limitation worth naming rather
/// than working around by calling into Providers.
/// </para>
/// <para>
/// <b>The category arrives as an identifier rather than a key, which is a real seam mismatch.</b>
/// Every other vertical publishes a human-readable category key that meets a member's interests in
/// the shared vocabulary; a GUID cannot. It was projected as-is so the field was populated and the
/// grouping worked, and enriching the contract with the key was named as the correct fix.
/// </para>
/// <para>
/// <b>PSR-1 made that fix.</b> The contract now carries <c>CategoryKey</c> beside the identifier,
/// and this consumer prefers it. The identifier remains the fallback for providers published before
/// the enrichment and for the case where the category could not be read at publication — a
/// candidate keyed by GUID still groups and still ranks, it just cannot meet a stated interest,
/// which is the behaviour this module had for every provider until now.
/// </para>
/// <para>
/// <b>The visibility string is Providers' own and is mapped conservatively.</b> Anything this module
/// does not recognise becomes members-only rather than public — the failure mode of guessing wrong
/// in the other direction is showing a private provider to the world.
/// </para>
/// </summary>
public sealed class ProviderPublishedConsumer
    : RecommendationEventConsumer<ProviderPublishedIntegrationEvent>
{
    public ProviderPublishedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Providers;

    protected override async Task HandleAsync(
        ProviderPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await Projector.ProjectAsync(
            new CandidateProjectionRequest
            {
                TargetType = RecommendationTargetType.Provider,
                TargetId = integrationEvent.ProviderId,
                SourceModule = SourceModule,
                Title = integrationEvent.Name,

                // The key when the publisher had one, the identifier when it did not. See the
                // class summary — the fallback is what makes this safe to deploy against a
                // projection built from pre-enrichment payloads.
                CategoryKey = integrationEvent.CategoryKey ?? integrationEvent.CategoryId.ToString(),
                CountryCode = integrationEvent.CountryCode,
                OwnerUserId = UserId.From(integrationEvent.OwnerId),
                PopularityScore = Jobs.JobPublishedConsumer.InitialPopularity,
                QualityScore = 0.4m,
                Visibility = MapVisibility(integrationEvent.Visibility),
                PublishedAt = integrationEvent.PublishedAt,
                SourceUpdatedAt = integrationEvent.PublishedAt,
                Attributes = [(InterestKind.Tag, integrationEvent.ProviderType)],
                ReplaceAttributes = true
            },
            cancellationToken);
    }

    /// <summary>
    /// Providers' visibility vocabulary to this module's.
    /// <para>
    /// Conservative on anything unrecognised — see the class summary.
    /// </para>
    /// </summary>
    private static CandidateVisibility MapVisibility(string visibility) =>
        visibility.Trim().ToLowerInvariant() switch
        {
            "public" => CandidateVisibility.Public,
            "verified" or "verifiedonly" => CandidateVisibility.Verified,
            "country" or "countryonly" => CandidateVisibility.CountryOnly,
            _ => CandidateVisibility.Members
        };
}
