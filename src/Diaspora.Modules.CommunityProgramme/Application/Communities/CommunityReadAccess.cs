using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Identity;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

/// <summary>A community the caller may read, with the caller's own standing in it.</summary>
internal sealed record ReadableCommunity(
    CommunityAggregate Community,
    CommunityMembership? Membership,
    CommunityCapabilitySet Capabilities);

/// <summary>
/// "May this caller read this community at all", for the routes added on 21 Oct 2026.
/// <para>
/// <b>The rule is <see cref="GetCommunityQueryHandler"/>'s, word for word</b>: staff read
/// everything; a pending community is its creator's alone; a public or unlisted one is anybody's,
/// signed in or not; a private or invite-only one is readable by somebody holding any row in it.
/// Every sub-resource of a community — its rules, notices, role holders and figures — answers with
/// the same not-found as the community itself when that rule says no, because a refusal would
/// confirm a private community exists.
/// </para>
/// </summary>
internal static class CommunityReadAccess
{
    public static async Task<ReadableCommunity?> ResolveAsync(
        string idOrSlug,
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        CancellationToken cancellationToken)
    {
        var community = await communities.FindByIdOrSlugAsync(idOrSlug, cancellationToken);

        if (community is null)
        {
            return null;
        }

        var membership = access.CallerId is { } viewer
            ? await memberships.FindAsync(community.Id, viewer, cancellationToken)
            : null;

        if (!CanRead(community, membership, access))
        {
            return null;
        }

        return new ReadableCommunity(
            community,
            membership,
            CommunityCapabilitySet.For(community, membership, access.IsStaff));
    }

    public static bool CanRead(
        CommunityAggregate community,
        CommunityMembership? membership,
        CommunityProgrammeAccessService access)
    {
        if (access.IsStaff)
        {
            return true;
        }

        if (community.Status == CommunityStatus.Pending)
        {
            // The founder holds a membership row from the first moment, so a row is the creator test
            // here too — and the stamp covers a pending community seeded without one.
            return membership is not null
                || (access.CallerId is { } caller && WasCreatedBy(community, caller));
        }

        return community.Visibility switch
        {
            CommunityVisibility.Public or CommunityVisibility.Unlisted => true,
            _ => membership is not null
        };
    }

    private static bool WasCreatedBy(CommunityAggregate community, UserId caller) =>
        community.CreatedBy is { Length: > 0 } stamp
        && (string.Equals(stamp, caller.ToString(), StringComparison.Ordinal)
            || string.Equals(stamp, caller.Value.ToString(), StringComparison.OrdinalIgnoreCase));
}
