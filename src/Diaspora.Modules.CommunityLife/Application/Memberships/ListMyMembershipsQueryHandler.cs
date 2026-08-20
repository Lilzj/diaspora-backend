using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>
/// The caller's own memberships, pending ones included.
/// <para>
/// Pending rows belong here and nowhere else: this is where somebody sees the invitation waiting for
/// them, and where they discover their application has not been answered.
/// </para>
/// </summary>
public sealed class ListMyMembershipsQueryHandler
    : IQueryHandler<ListMyMembershipsQuery, MembershipPageResponse>
{
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly CommunityLifeAuthorizationService _authorization;

    public ListMyMembershipsQueryHandler(
        IOrganizationMembershipRepository memberships,
        CommunityLifeAuthorizationService authorization)
    {
        _memberships = memberships;
        _authorization = authorization;
    }

    public async Task<Result<MembershipPageResponse>> Handle(
        ListMyMembershipsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<MembershipPageResponse>(caller.Error);
        }

        var page = await _memberships.SearchAsync(
            new MembershipQueryFilter
            {
                MemberUserId = caller.Value,
                Statuses =
                [
                    MembershipStatus.Active,
                    MembershipStatus.Requested,
                    MembershipStatus.Invited,
                    MembershipStatus.Lapsed
                ],
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
}
