using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Catalog;

/// <summary>
/// Reads every attribute definition that applies to a category, ancestors included.
/// <para>
/// Ancestors included, because a definition on <c>/electronics/</c> applies to every phone beneath
/// it — declaring "brand" once is the whole point of scoping by path rather than by leaf.
/// </para>
/// </summary>
public sealed class ListCategoryAttributesQueryHandler
    : IQueryHandler<ListCategoryAttributesQuery, IReadOnlyList<ListingAttributeDefinitionResponse>>
{
    private readonly ListingCategoryResolver _categories;
    private readonly ListingAttributeValidator _attributes;

    public ListCategoryAttributesQueryHandler(
        ListingCategoryResolver categories, ListingAttributeValidator attributes)
    {
        _categories = categories;
        _attributes = attributes;
    }

    public async Task<Result<IReadOnlyList<ListingAttributeDefinitionResponse>>> Handle(
        ListCategoryAttributesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var category = await _categories.FindAsync(query.CategoryKey, cancellationToken);

        if (category is null)
        {
            return Result.Failure<IReadOnlyList<ListingAttributeDefinitionResponse>>(
                MarketplaceErrors.CategoryNotFound);
        }

        var definitions = await _attributes.GetDefinitionsAsync(
            category.Path, cancellationToken);

        return Result.Success<IReadOnlyList<ListingAttributeDefinitionResponse>>(
            [.. definitions.Where(definition => definition.IsActive)
                .Select(CatalogMapper.ToResponse)]);
    }
}
