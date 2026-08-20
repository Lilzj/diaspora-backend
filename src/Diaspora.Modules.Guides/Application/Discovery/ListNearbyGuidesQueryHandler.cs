using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Serves the nearby shelf.
/// <para>
/// <b>B8 parses no radius, resolves no city name and computes no distance.</b> The whole request
/// is handed to <c>GuideGeographicScopeAdapter</c>, which is this module's one contact point with
/// B4, and the resolved scope is applied to the filter by the same class. "Near me" therefore
/// means exactly what it means on every other surface of the platform, and it keeps meaning that
/// when B4 changes its mind.
/// </para>
/// <para>
/// A guide is jurisdiction-scoped rather than point-scoped — it has a country and optionally an
/// area, never a coordinate — so a resolved scope narrows this list by country. An unbounded
/// scope narrows nothing, which is "everywhere" rather than "nowhere": a member whose location
/// could not be resolved still gets a useful screen.
/// </para>
/// </summary>
public sealed class ListNearbyGuidesQueryHandler
    : IQueryHandler<ListNearbyGuidesQuery, CursorPagedResult<GuideCardResponse>>
{
    private readonly IGuideRepository _guides;
    private readonly GuideGeographicScopeAdapter _geography;
    private readonly GuideAuthorizationService _authorization;

    public ListNearbyGuidesQueryHandler(
        IGuideRepository guides,
        GuideGeographicScopeAdapter geography,
        GuideAuthorizationService authorization)
    {
        _guides = guides;
        _geography = geography;
        _authorization = authorization;
    }

    public async Task<Result<CursorPagedResult<GuideCardResponse>>> Handle(
        ListNearbyGuidesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var request = query.Request;

        var limit = Math.Clamp(
            request.Limit ?? GuideLimits.DefaultPageSize, 1, GuideLimits.MaxPageSize);

        var scope = await _geography.ResolveAsync(
            request, _authorization.CallerId, cancellationToken);

        if (scope.IsFailure)
        {
            return Result.Failure<CursorPagedResult<GuideCardResponse>>(scope.Error);
        }

        var filter = new GuideQueryFilter
        {
            Statuses = [GuideStatus.Published],
            CategoryKey = string.IsNullOrWhiteSpace(request.Category)
                ? null
                : request.Category.Trim().ToLowerInvariant(),
            ViewerId = _authorization.CallerId,
            ViewerIsStaff = _authorization.IsStaff,
            Sort = GuideSortOrder.Newest,
            Limit = limit
        };

        var scoped = GuideGeographicScopeAdapter.Apply(filter, scope.Value);

        var page = await _guides.QueryAsync(scoped, query.Cursor, limit, cancellationToken);

        return Result.Success(CursorPagedResult<GuideCardResponse>.Create(
            page.Items.Select(GuideCardMapper.ToCard).ToList(),
            page.NextCursor,
            page.HasMore));
    }
}
