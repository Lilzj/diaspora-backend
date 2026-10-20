using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Jobs;

/// <summary>
/// A vacancy became visible.
/// <para>
/// <b>The canonical projection consumer, and worth reading as the template for the other ten.</b>
/// Everything the ranking strategies need about a job comes off this payload — its category, its
/// tags, its skills, where it is, whether the employer is verified, when it closes — and nothing
/// here calls back into the Jobs module. If a strategy needed a field the contract does not carry,
/// the fix would be to enrich the contract.
/// </para>
/// <para>
/// <b>Skills and tags are projected into the same vocabulary the profile uses.</b> That is the whole
/// mechanism of interest matching: a member's affinity for <c>react</c> and a job's <c>react</c>
/// skill meet as one set intersection rather than as a per-vertical comparison.
/// </para>
/// <para>
/// <b><c>ClosingAt</c> becomes the expiry, which is what stops the platform recommending a closed
/// vacancy.</b> The availability policy reads it, and the pool query filters on it — belt and braces,
/// because a candidate applying for a job that closed last week is the single most wasteful thing
/// this module can cause.
/// </para>
/// <para>
/// <b>Popularity starts at a modest constant rather than zero.</b> A brand-new posting has no
/// engagement, and a zero would rank it below everything on the surface until somebody found it —
/// which nobody would, because it is ranked below everything. Jobs republishes engagement figures
/// separately.
/// </para>
/// </summary>
public sealed class JobPublishedConsumer : RecommendationEventConsumer<JobPublishedIntegrationEvent>
{
    /// <summary>What a newly published candidate starts at. See the class summary.</summary>
    internal const decimal InitialPopularity = 0.3m;

    public JobPublishedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Jobs;

    protected override async Task HandleAsync(
        JobPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var attributes = new List<(InterestKind Kind, string Key)>();
        attributes.AddRange(Attributes(InterestKind.Tag, integrationEvent.Tags));
        attributes.AddRange(Attributes(InterestKind.Skill, integrationEvent.SkillKeys));

        // Two facts a diaspora candidate filters on before anything else, projected as tags so the
        // interest strategy can match them without knowing what a work visa is.
        if (integrationEvent.OffersVisaSponsorship)
        {
            attributes.Add((InterestKind.Tag, "visa-sponsorship"));
        }

        if (integrationEvent.OffersRelocationAssistance)
        {
            attributes.Add((InterestKind.Tag, "relocation-assistance"));
        }

        attributes.Add((InterestKind.Tag, integrationEvent.WorkplaceType));
        attributes.Add((InterestKind.Tag, integrationEvent.JobType));
        attributes.Add((InterestKind.Tag, integrationEvent.ExperienceLevel));

        await Projector.ProjectAsync(
            new CandidateProjectionRequest
            {
                TargetType = RecommendationTargetType.Job,
                TargetId = integrationEvent.JobId,
                SourceModule = SourceModule,
                Title = integrationEvent.Title,
                Summary = integrationEvent.Summary,
                Slug = integrationEvent.Slug,
                CategoryKey = integrationEvent.CategoryKey,
                CountryCode = integrationEvent.CountryCode,
                RegionName = integrationEvent.RegionName,
                CityName = integrationEvent.CityName,
                // The member behind the employer profile. Jobs never recorded an owner, so a member's
                // own vacancy could be recommended back to them; see ListingPublishedConsumer.
                OwnerUserId = integrationEvent.EmployerUserId is { } employerUserId
                    ? UserId.From(employerUserId)
                    : null,
                IsVerified = integrationEvent.IsEmployerVerified,
                PopularityScore = InitialPopularity,
                QualityScore = integrationEvent.IsEmployerVerified ? 0.7m : 0.4m,
                Visibility = CandidateVisibility.Members,
                PublishedAt = integrationEvent.PublishedAt,
                SourceUpdatedAt = integrationEvent.PublishedAt,

                // See the class summary. The single most important field on this payload for the
                // module's credibility.
                ExpiresAt = integrationEvent.ClosingAt,
                Attributes = attributes,
                ReplaceAttributes = true
            },
            cancellationToken);
    }
}
