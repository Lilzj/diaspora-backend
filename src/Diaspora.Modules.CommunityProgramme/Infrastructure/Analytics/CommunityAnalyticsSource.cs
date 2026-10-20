using Diaspora.Contracts.CommunityProgramme.V1.Analytics;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityProgramme.Infrastructure.Analytics;

/// <summary>
/// A community's membership figures, contributed to the Analytics module — FRONTEND-PHASE15-GAP-003.
/// <para>
/// <b>This module answers because it owns the membership table and the rule for who may read a
/// community's private statistics.</b> The rule is <see cref="CommunityCapabilitySet"/>'s
/// <c>CanViewPrivateStatistics</c> — moderators, administrators and the owner, and staff — applied
/// here rather than restated in Analytics, so the two cannot drift.
/// </para>
/// <para>
/// Read-only, and it publishes nothing: no member identifier leaves this class.
/// </para>
/// </summary>
public sealed class CommunityAnalyticsSource : ICommunityAnalyticsSource
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;
    private readonly DiasporaDbContext _dbContext;

    public CommunityAnalyticsSource(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        DiasporaDbContext dbContext)
    {
        _communities = communities;
        _memberships = memberships;
        _access = access;
        _dbContext = dbContext;
    }

    public async Task<CommunityAnalyticsReading?> ReadAsync(
        string idOrSlug,
        Guid viewerUserId,
        bool viewerIsPlatformAnalyst,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idOrSlug))
        {
            return null;
        }

        var community = await _communities
            .FindByIdOrSlugAsync(idOrSlug.Trim(), cancellationToken)
            .ConfigureAwait(false);

        if (community is null)
        {
            return null;
        }

        var viewership = viewerUserId == Guid.Empty
            ? null
            : await _memberships
                .FindAsync(community.Id, UserId.From(viewerUserId), cancellationToken)
                .ConfigureAwait(false);

        var capabilities = CommunityCapabilitySet.For(
            community, viewership, isStaff: viewerIsPlatformAnalyst || _access.IsStaff);

        if (!capabilities.CanViewPrivateStatistics)
        {
            return new CommunityAnalyticsReading(community.Id.Value, community.Name, false, 0, [], []);
        }

        var memberships = _dbContext.Set<CommunityMembership>()
            .AsNoTracking()
            .Where(membership => membership.CommunityId == community.Id);

        var current = await memberships
            .LongCountAsync(
                membership => membership.State == CommunityMembershipState.Member
                    || membership.State == CommunityMembershipState.VerifiedMember,
                cancellationToken)
            .ConfigureAwait(false);

        // A join is a membership that began: one still current, or one that has since ended. A
        // pending, declined or follow-only row never began, whatever its timestamps say.
        var joined = await memberships
            .Where(membership =>
                (membership.State == CommunityMembershipState.Member
                    || membership.State == CommunityMembershipState.VerifiedMember
                    || membership.State == CommunityMembershipState.Left
                    || membership.State == CommunityMembershipState.Revoked)
                && membership.JoinedAt != null
                && membership.JoinedAt >= from
                && membership.JoinedAt < to)
            .Select(membership => membership.JoinedAt!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ended = await memberships
            .Where(membership =>
                (membership.State == CommunityMembershipState.Left
                    || membership.State == CommunityMembershipState.Revoked)
                && membership.EndedAt != null
                && membership.EndedAt >= from
                && membership.EndedAt < to)
            .Select(membership => membership.EndedAt!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new CommunityAnalyticsReading(
            community.Id.Value, community.Name, true, current, joined, ended);
    }
}
