using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>
/// An organization's members.
/// <para>
/// <b>Who may read the list is the body's decision, and the default is closed.</b> Who belongs to a
/// support group, a political association or a congregation is among the most consequential facts
/// this module holds about a person, and a directory that leaked it would do so to everybody holding
/// a link. A body that publishes its membership shows it to its own members; an administrator sees
/// it either way.
/// </para>
/// <para>
/// <b>Pending rows are for administrators only</b>, whatever the body's list policy. Somebody's
/// application is not yet a membership, and showing it to the room would tell everybody who asked
/// and was not answered.
/// </para>
/// </summary>
public sealed class ListMembershipsQueryHandler
    : IQueryHandler<ListMembershipsQuery, MembershipPageResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly CommunityLifeAuthorizationService _authorization;

    public ListMembershipsQueryHandler(
        IOrganizationRepository organizations,
        IOrganizationMembershipRepository memberships,
        CommunityLifeAuthorizationService authorization)
    {
        _organizations = organizations;
        _memberships = memberships;
        _authorization = authorization;
    }

    public async Task<Result<MembershipPageResponse>> Handle(
        ListMembershipsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var organizationId = OrganizationId.From(query.OrganizationId);

        var organization = await _organizations.GetAsync(organizationId, cancellationToken);

        if (organization is null)
        {
            return Result.Failure<MembershipPageResponse>(
                CommunityLifeErrors.OrganizationNotFound);
        }

        var viewer = await _authorization.FindMembershipAsync(organizationId, cancellationToken);

        var isAdministrator = _authorization.IsStaff
            || organization.CreatedByUserId == _authorization.CallerId
            || viewer?.HasAtLeast(MembershipRole.Administrator) == true;

        if (!isAdministrator
            && !(organization.MembershipPolicy.IsMembershipPublic && viewer?.IsCurrent == true))
        {
            return Result.Failure<MembershipPageResponse>(
                CommunityLifeErrors.NotOrganizationMember);
        }

        var statuses = ResolveStatuses(query, isAdministrator);

        var page = await _memberships.SearchAsync(
            new MembershipQueryFilter
            {
                OrganizationId = organizationId,
                Statuses = statuses,
                MinimumRole = CommunityLifeSharedMapper.ParseEnum<MembershipRole>(
                    query.MinimumRole),
                SearchTerm = query.SearchTerm,
                Cursor = query.Cursor,
                Limit = Math.Clamp(query.Limit ?? 20, 1, CommunityLifeLimits.MaxPageSize)
            },
            cancellationToken);

        return Result.Success(new MembershipPageResponse
        {
            Items = [.. page.Items.Select(OrganizationMapper.ToResponse)],
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }

    /// <summary>
    /// The statuses this caller is allowed to ask for, whatever they asked for.
    /// <para>
    /// A non-administrator's request is narrowed to active rather than refused, so a client that
    /// sends the administrator's filter gets the member's answer instead of an error it cannot act
    /// on.
    /// </para>
    /// </summary>
    private static List<MembershipStatus> ResolveStatuses(
        ListMembershipsQuery query, bool isAdministrator)
    {
        if (!isAdministrator || query.Statuses is not { Count: > 0 })
        {
            return [MembershipStatus.Active];
        }

        var parsed = query.Statuses
            .Select(CommunityLifeSharedMapper.ParseEnum<MembershipStatus>)
            .OfType<MembershipStatus>()
            .Distinct()
            .ToList();

        return parsed.Count == 0 ? [MembershipStatus.Active] : parsed;
    }
}
