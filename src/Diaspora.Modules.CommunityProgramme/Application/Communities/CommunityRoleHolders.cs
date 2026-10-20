using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Who runs a community — FRONTEND-PHASE13-GAP-018/019, 21 Oct 2026.
//
// **A listing of office holders, not of members.** The member roll stays members-only
// (ListCommunityMembersQuery). This answers "who are the moderators and ambassadors here", which a
// person deciding whether to join is entitled to ask: a role is an office in the community, and an
// office nobody can name is one nobody can contact or hold to account.
//
// **Still bounded by the holder's own visibility choice.** A role can only be held on a membership
// that is at least visible to the community (`CommunityMembership.Grant` refuses a private one). A
// holder who chose Public is listed to everybody who may read the community; one who chose
// CommunityOnly is listed to its current members alone. Staff get no wider view — the line
// `CommunityCapabilitySet` draws for the roll applies here too.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>A community's office holders, optionally of one role.</summary>
public sealed record ListCommunityRoleHoldersQuery : IQuery<IReadOnlyList<CommunityRoleHolderResponse>>
{
    public required string IdOrSlug { get; init; }

    /// <summary>Moderator, Ambassador, Administrator or Owner. Member, or none, lists every office holder.</summary>
    public string? Role { get; init; }
}

public sealed class ListCommunityRoleHoldersQueryHandler
    : IQueryHandler<ListCommunityRoleHoldersQuery, IReadOnlyList<CommunityRoleHolderResponse>>
{
    private const int Limit = 100;

    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityMembershipStatistics _statistics;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;

    public ListCommunityRoleHoldersQueryHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityMembershipStatistics statistics,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles)
    {
        _communities = communities;
        _memberships = memberships;
        _statistics = statistics;
        _access = access;
        _profiles = profiles;
    }

    public async Task<Result<IReadOnlyList<CommunityRoleHolderResponse>>> Handle(
        ListCommunityRoleHoldersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var role = CommunityRoleFilter.Parse(query.Role);

        if (role.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CommunityRoleHolderResponse>>(role.Error);
        }

        var readable = await CommunityReadAccess.ResolveAsync(
            query.IdOrSlug, _communities, _memberships, _access, cancellationToken);

        if (readable is null)
        {
            return Result.Failure<IReadOnlyList<CommunityRoleHolderResponse>>(CommunityErrors.NotFound);
        }

        var holders = await _statistics.ListRoleHoldersAsync(
            readable.Community.Id,
            role.Value,
            includeCommunityOnly: readable.Membership?.IsCurrentMember == true,
            Limit,
            cancellationToken);

        var names = holders.Count == 0
            ? new Dictionary<Guid, MemberProfile>()
            : await _profiles.ResolveAsync([.. holders.Select(holder => holder.MemberId)], cancellationToken);

        // A holder with no profile is an account that no longer exists; there is nobody to name.
        return Result.Success<IReadOnlyList<CommunityRoleHolderResponse>>(
        [
            .. holders
                .Where(holder => names.ContainsKey(holder.MemberId.Value))
                .Select(holder => new CommunityRoleHolderResponse
                {
                    UserId = holder.MemberId.Value,
                    DisplayName = names[holder.MemberId.Value].DisplayName,
                    Role = role.Value ?? CommunityNoticeAccess.HighestRole(holder).ToString(),
                    Since = holder.JoinedAt
                })
        ]);
    }
}

/// <summary>The <c>?role=</c> filter, shared by the member roll and the office-holder listing.</summary>
internal static class CommunityRoleFilter
{
    /// <summary>Null for no filter (absent, or Member, which every member is).</summary>
    public static Result<string?> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success<string?>(null);
        }

        if (!Enum.TryParse<CommunityMembershipRole>(value.Trim(), ignoreCase: true, out var role)
            || !Enum.IsDefined(role))
        {
            return Result.Failure<string?>(CommunityGovernanceErrors.RoleFilterUnknown);
        }

        return Result.Success<string?>(role == CommunityMembershipRole.Member ? null : role.ToString());
    }
}
