using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Catalog;

/// <summary>
/// Adds a node to the commerce taxonomy.
/// <para>
/// The administrative counterpart to <c>IListingCategoryContributor</c>: a contributor ships a
/// taxonomy with the code, and this is how an operator adds the one their market needs without
/// waiting for a release.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.ManageCategories)]
[Auditable]
public sealed record CreateCategoryCommand : ICommand<ListingCategoryResponse>
{
    public required CreateCategoryRequest Request { get; init; }
}
