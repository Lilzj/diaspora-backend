using Diaspora.Modules.Marketplace.Domain.Catalog;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// Turns a category key from the wire into a node from the catalogue.
/// <para>
/// <b>Every write path that touches a category comes through here, and that is the point.</b> A
/// listing filed under a key nobody ever defined is a listing that appears in no category page and
/// no facet — invisible in exactly the way a seller will not notice until nothing sells.
/// </para>
/// <para>
/// Resolving also yields the node's materialised path, which the attribute validator needs: the
/// definitions that apply to a listing are the ones scoped to its category or to any ancestor, and
/// that is a prefix comparison on the path rather than a walk up the tree.
/// </para>
/// </summary>
public sealed class ListingCategoryResolver
{
    private readonly IListingCategoryRepository _categories;

    public ListingCategoryResolver(IListingCategoryRepository categories) =>
        _categories = categories;

    /// <summary>
    /// The node a key names, provided it is still open for new listings.
    /// <para>
    /// A retired category is a distinct failure from a missing one: the seller has not made a typo,
    /// the platform has stopped accepting listings there, and telling them so is the difference
    /// between a message they can act on and one they cannot.
    /// </para>
    /// </summary>
    public async Task<Result<ListingCategory>> ResolveActiveAsync(
        string? categoryKey, CancellationToken cancellationToken = default)
    {
        var normalized = ListingCategory.NormalizeKey(categoryKey);

        if (normalized is null)
        {
            return Result.Failure<ListingCategory>(MarketplaceErrors.CategoryKeyInvalid);
        }

        var category = await _categories.GetByKeyAsync(normalized, cancellationToken);

        if (category is null)
        {
            return Result.Failure<ListingCategory>(MarketplaceErrors.CategoryNotFound);
        }

        return category.IsActive
            ? Result.Success(category)
            : Result.Failure<ListingCategory>(MarketplaceErrors.CategoryInactive);
    }

    /// <summary>
    /// The node a key names, retired or not. What a read path uses — a listing filed under a
    /// category that has since been retired must still render its name.
    /// </summary>
    public async Task<ListingCategory?> FindAsync(
        string? categoryKey, CancellationToken cancellationToken = default)
    {
        var normalized = ListingCategory.NormalizeKey(categoryKey);

        return normalized is null
            ? null
            : await _categories.GetByKeyAsync(normalized, cancellationToken);
    }

    /// <summary>
    /// Several nodes at once, keyed. What a results page hydrates its category chips from without
    /// a query per card.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, ListingCategory>> FindManyAsync(
        IReadOnlyCollection<string> categoryKeys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categoryKeys);

        var normalized = categoryKeys
            .Select(ListingCategory.NormalizeKey)
            .Where(key => key is not null)
            .Select(key => key!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return normalized.Count == 0
            ? new Dictionary<string, ListingCategory>(StringComparer.Ordinal)
            : await _categories.GetByKeysAsync(normalized, cancellationToken);
    }

    /// <summary>
    /// The path prefix a subtree filter narrows by, from either a key or a path the client sent.
    /// Null when neither resolves, which narrows nothing rather than narrowing to nothing.
    /// </summary>
    public async Task<string?> ResolvePathPrefixAsync(
        string? categoryKeyOrPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(categoryKeyOrPath))
        {
            return null;
        }

        if (categoryKeyOrPath.Contains('/', StringComparison.Ordinal))
        {
            var path = categoryKeyOrPath.Trim().ToLowerInvariant();

            if (!path.StartsWith('/'))
            {
                path = '/' + path;
            }

            return path.EndsWith('/') ? path : path + '/';
        }

        var category = await FindAsync(categoryKeyOrPath, cancellationToken);

        return category?.Path;
    }
}
