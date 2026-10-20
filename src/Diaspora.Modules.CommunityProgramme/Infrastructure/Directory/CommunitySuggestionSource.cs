using Diaspora.Contracts.CommunityProgramme.V1.Directory;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityProgramme.Infrastructure.Directory;

/// <summary>
/// CommunityProgramme's half of the community suggestions seam.
/// <para>
/// <b>The directory's own search, so the two can never disagree</b> about what is discoverable:
/// public, live, and — when a country is named — declaring a presence there. Suspended communities
/// stay findable in the directory so members can read why; they are never <em>suggested</em>.
/// </para>
/// <para>
/// <b>Standing is excluded in the query, not after it.</b> Dropping joined rows from a page would
/// return a short page, and a member who belongs to most of the newest communities would be told
/// there is nothing else.
/// </para>
/// </summary>
public sealed class CommunitySuggestionSource : ICommunitySuggestionSource
{
    /// <summary>Any standing that makes "join this" the wrong suggestion.</summary>
    private static readonly CommunityMembershipState[] Excluded =
    [
        CommunityMembershipState.PendingApproval,
        CommunityMembershipState.Member,
        CommunityMembershipState.VerifiedMember,
        CommunityMembershipState.Declined,
        CommunityMembershipState.Revoked
    ];

    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;

    public CommunitySuggestionSource(
        ICommunityRepository communities, ICommunityMembershipRepository memberships)
    {
        _communities = communities;
        _memberships = memberships;
    }

    public async Task<IReadOnlyList<CommunitySuggestionCandidate>> ListAsync(
        CommunitySuggestionQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var standing = await _memberships.FindForMemberAsync(
            UserId.From(query.ViewerUserId), null, cancellationToken);

        var excluded = standing
            .Where(entry => Excluded.Contains(entry.Value.State))
            .Select(entry => entry.Key)
            .ToList();

        var page = await _communities.SearchAsync(
            new CommunityDirectoryFilter
            {
                PresenceCountryCode = string.IsNullOrWhiteSpace(query.PresenceCountryCode)
                    ? null
                    : query.PresenceCountryCode.Trim().ToUpperInvariant(),
                ExcludeCommunityIds = excluded,
                Limit = Math.Clamp(query.Take, 1, CommunityProgrammeLimits.MaxPageSize)
            },
            cancellationToken);

        return
        [
            .. page.Items
                .Where(community => community.Status == CommunityStatus.Active)
                .Select(community => new CommunitySuggestionCandidate
                {
                    CommunityId = community.Id.Value,
                    Name = community.Name,
                    Slug = community.Slug.Value,
                    Tagline = community.Tagline,
                    Type = community.Type.ToString(),
                    OriginCountryCode = community.OriginCountryCode,
                    OriginRegionName = community.OriginRegionName,
                    PresenceCountryCodes = [.. community.PresenceCountryCodes],
                    PrimaryLanguage = community.PrimaryLanguage,
                    SupportedLanguages = [.. community.SupportedLanguages],
                    LogoMediaId = community.LogoMediaId,
                    CreatedAt = community.CreatedAt
                })
        ];
    }
}
