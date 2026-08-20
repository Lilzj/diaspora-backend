using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.CommunityLife;

/// <summary>
/// A diaspora organization became visible.
/// <para>
/// <b>The one contract that publishes a real engagement figure at publication time.</b> Member count
/// is a genuine measure of how established an organization is, and it is available immediately — so
/// unlike every other vertical, an organization does not start at a placeholder popularity.
/// </para>
/// <para>
/// <b>Languages become attributes rather than the candidate's language.</b> An organization can
/// operate in three; the candidate's single language field cannot hold that, and picking one would
/// mean the interest strategy scoring a trilingual organization as though it excluded two thirds of
/// its own members.
/// </para>
/// <para>
/// <b>B13.1 gave this projection an owner, and it was the only vertical without one.</b> Every
/// other published consumer set <c>OwnerUserId</c> from its contract; this one could not, because
/// the organization contract named no member. The consequence was small and persistent — an
/// organization could be recommended back to the person who runs it, which every other surface
/// already knew not to do.
/// </para>
/// </summary>
public sealed class OrganizationPublishedConsumer
    : RecommendationEventConsumer<OrganizationPublishedIntegrationEvent>
{
    /// <summary>Members at which the popularity score saturates.</summary>
    private const double SaturationMembers = 5_000d;

    public OrganizationPublishedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.CommunityLife;

    protected override async Task HandleAsync(
        OrganizationPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Several at once, so attributes rather than the language field. See the class summary.
        var attributes = Attributes(InterestKind.Language, integrationEvent.Languages).ToList();
        attributes.Add((InterestKind.Tag, integrationEvent.OrganizationTypeKey));

        await Projector.ProjectAsync(
            new CandidateProjectionRequest
            {
                TargetType = RecommendationTargetType.Organization,
                TargetId = integrationEvent.OrganizationId,
                SourceModule = SourceModule,
                Title = integrationEvent.Name,
                Summary = integrationEvent.Summary,
                Slug = integrationEvent.Slug,
                MediaReference = integrationEvent.LogoMediaId?.ToString(),
                CategoryKey = integrationEvent.OrganizationTypeKey,
                CountryCode = integrationEvent.CountryCode,
                RegionName = integrationEvent.RegionName,
                CityName = integrationEvent.CityName,
                NeighborhoodName = integrationEvent.NeighborhoodName,
                IsVerified = integrationEvent.IsVerified,

                // Null on an organization published before B13.1. The projection is still built;
                // it simply cannot be excluded from its administrator's own recommendations until
                // the next republication carries the field.
                OwnerUserId = integrationEvent.OrganizerUserId is { } organizerUserId
                    ? UserId.From(organizerUserId)
                    : null,

                // A real figure at publication time — see the class summary.
                PopularityScore = ToScore(integrationEvent.MemberCount),
                EngagementCount = integrationEvent.MemberCount,
                QualityScore = integrationEvent.IsVerified ? 0.75m : 0.4m,
                Visibility = CandidateVisibility.Members,
                PublishedAt = integrationEvent.PublishedAt,
                SourceUpdatedAt = integrationEvent.PublishedAt,
                Attributes = attributes,
                ReplaceAttributes = true
            },
            cancellationToken);
    }

    /// <summary>
    /// Members to a 0–1 score, logarithmically.
    /// <para>
    /// Organization size is power-law distributed like guide readership: the largest diaspora
    /// association in a country has thousands of times the membership of a neighbourhood group, and a
    /// linear scale would put every small organization at zero.
    /// </para>
    /// </summary>
    private static decimal ToScore(int memberCount)
    {
        if (memberCount <= 0)
        {
            return 0m;
        }

        var score = Math.Log10(memberCount + 1) / Math.Log10(SaturationMembers);

        return decimal.Round((decimal)Math.Clamp(score, 0d, 1d), 6);
    }
}
