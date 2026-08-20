using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.CommunityLife;

/// <summary>
/// Somebody joined an organization.
/// <para>
/// <b>Two things at once: a strong commitment signal, and a live membership figure.</b> Joining is
/// the clearest statement of belonging the platform records, and the count that arrives with it is
/// how an organization's popularity stays current without anybody publishing a per-view event.
/// </para>
/// <para>
/// <b>Recorded even when the membership is private, because the two privacies are different.</b>
/// <c>IsMembershipPublic</c> governs whether other members see the membership on a profile; this
/// module is recording, for the member's own benefit, that they are interested in this kind of
/// organization. Withholding it would degrade their own recommendations to protect them from
/// themselves.
/// </para>
/// </summary>
public sealed class OrganizationMemberJoinedConsumer
    : RecommendationEventConsumer<OrganizationMemberJoinedIntegrationEvent>
{
    private const double SaturationMembers = 5_000d;

    public OrganizationMemberJoinedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.CommunityLife;

    protected override async Task HandleAsync(
        OrganizationMemberJoinedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Recorded regardless of whether the membership is publicly visible. See the class summary.
        await RecordAsync(
            Signal(
                UserId.From(integrationEvent.MemberUserId),
                SignalKind.Joined,
                integrationEvent.JoinedAt,
                RecommendationTargetType.Organization,
                integrationEvent.OrganizationId),
            cancellationToken);

        await Projector.ProjectAsync(
            new CandidateProjectionRequest
            {
                TargetType = RecommendationTargetType.Organization,
                TargetId = integrationEvent.OrganizationId,
                SourceModule = SourceModule,
                PopularityScore = ToScore(integrationEvent.MemberCount),
                EngagementCount = integrationEvent.MemberCount,
                SourceUpdatedAt = integrationEvent.JoinedAt
            },
            cancellationToken);
    }

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
