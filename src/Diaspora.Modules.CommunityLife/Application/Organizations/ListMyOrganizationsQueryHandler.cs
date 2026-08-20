using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// The bodies the caller runs or belongs to, drafts included.
/// <para>
/// <b>Assembled from the caller's memberships rather than from a search.</b> The set is small — a
/// member belongs to a handful of organizations, not a page of them — and reading it this way means
/// a draft appears the moment its owner membership commits, without the directory having to expose
/// an "include non-public" switch that anybody could send.
/// </para>
/// <para>
/// The creator is included even where a membership row is missing, which covers the same instant the
/// authorization service covers: between a draft committing and its owner membership arriving.
/// </para>
/// </summary>
public sealed class ListMyOrganizationsQueryHandler
    : IQueryHandler<ListMyOrganizationsQuery, OrganizationSearchResultResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly CommunityLifeAuthorizationService _authorization;

    public ListMyOrganizationsQueryHandler(
        IOrganizationRepository organizations,
        CommunityLifeAuthorizationService authorization)
    {
        _organizations = organizations;
        _authorization = authorization;
    }

    public async Task<Result<OrganizationSearchResultResponse>> Handle(
        ListMyOrganizationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<OrganizationSearchResultResponse>(caller.Error);
        }

        var limit = Math.Clamp(query.Limit ?? 20, 1, CommunityLifeLimits.MaxPageSize);

        var ids = await _authorization.CallerOrganizationsAsync(cancellationToken);

        var organizations = ids.Count == 0
            ? []
            : await _organizations.FindManyAsync(ids, cancellationToken);

        var ordered = organizations
            .OrderByDescending(organization =>
                organization.LastActivityAt ?? organization.CreatedAt)
            .ThenBy(organization => organization.Id.Value)
            .Take(limit)
            .ToList();

        return Result.Success(new OrganizationSearchResultResponse
        {
            Items = [.. ordered.Select(OrganizationMapper.ToSummary)],

            // Deliberately unpaged. Somebody who belongs to more than a hundred organizations is a
            // case that does not exist, and the membership read is already capped there.
            NextCursor = null,
            HasMore = organizations.Count > ordered.Count,
            TotalCount = organizations.Count
        });
    }
}
