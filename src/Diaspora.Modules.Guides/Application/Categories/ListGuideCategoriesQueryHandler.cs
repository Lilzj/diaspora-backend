using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Categories;

/// <summary>
/// Serves the catalogue.
/// <para>
/// Deactivated categories are returned only to staff. A member offered a deactivated category
/// would file a guide under something nobody browses, and would have no way to know why their
/// guide never found readers.
/// </para>
/// <para>
/// Small by design and never paged — the catalogue is tens of rows, and a cursor over it would
/// be ceremony that every client then has to implement.
/// </para>
/// </summary>
public sealed class ListGuideCategoriesQueryHandler
    : IQueryHandler<ListGuideCategoriesQuery, IReadOnlyList<GuideCategoryResponse>>
{
    private readonly GuideCategoryResolver _categories;
    private readonly GuideAuthorizationService _authorization;

    public ListGuideCategoriesQueryHandler(
        GuideCategoryResolver categories, GuideAuthorizationService authorization)
    {
        _categories = categories;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<GuideCategoryResponse>>> Handle(
        ListGuideCategoriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var includeInactive = query.IncludeInactive && _authorization.IsStaff;

        var categories = await _categories.ListAsync(includeInactive, cancellationToken);

        var visible = categories.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.ParentKey))
        {
            var parentKey = query.ParentKey.Trim().ToLowerInvariant();

            visible = visible.Where(category =>
                string.Equals(category.ParentKey, parentKey, StringComparison.Ordinal));
        }

        return Result.Success<IReadOnlyList<GuideCategoryResponse>>(
            visible.Select(GuideCategoryMapper.ToResponse).ToList());
    }
}
