using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Catalog;

/// <summary>
/// Renames or retires a category.
/// <para>
/// The key never moves — listings store it, so changing it would orphan every listing filed under
/// the node. Retiring takes it off the create form and leaves a decade of listings readable.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.ManageCategories)]
[Auditable]
public sealed record UpdateCategoryCommand : ICommand<ListingCategoryResponse>
{
    public required string Key { get; init; }

    public required UpdateCategoryRequest Request { get; init; }
}
