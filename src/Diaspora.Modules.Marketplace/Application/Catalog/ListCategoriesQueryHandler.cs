using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Catalog;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Catalog;

/// <summary>
/// Reads the category catalogue.
/// <para>
/// A caching decorator can sit in front of the repository without any of this changing, which is
/// why the handler says nothing about how it is fetched.
/// </para>
/// </summary>
public sealed class ListCategoriesQueryHandler
    : IQueryHandler<ListCategoriesQuery, IReadOnlyList<ListingCategoryResponse>>
{
    private readonly IListingCategoryRepository _categories;

    public ListCategoriesQueryHandler(IListingCategoryRepository categories) =>
        _categories = categories;

    public async Task<Result<IReadOnlyList<ListingCategoryResponse>>> Handle(
        ListCategoriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var categories = await _categories.GetAllAsync(query.IncludeInactive, cancellationToken);

        return Result.Success<IReadOnlyList<ListingCategoryResponse>>(
            [.. categories.Select(CatalogMapper.ToResponse)]);
    }
}
