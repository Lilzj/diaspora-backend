using Diaspora.Modules.Marketplace.Domain.Identifiers;

namespace Diaspora.Modules.Marketplace.Domain.Catalog;

/// <summary>
/// How the application layer reaches the category tree.
/// <para>
/// Read whole rather than paged: it is a few hundred rows that change when code ships, and a
/// caching decorator can sit in front of this without any caller changing.
/// </para>
/// </summary>
public interface IListingCategoryRepository
{
    Task<ListingCategory?> GetAsync(
        ListingCategoryId id, CancellationToken cancellationToken = default);

    /// <summary>The node a listing's stored key names.</summary>
    Task<ListingCategory?> GetByKeyAsync(
        string key, CancellationToken cancellationToken = default);

    /// <summary>Several nodes at once. What a results page's category chips are hydrated from.</summary>
    Task<IReadOnlyDictionary<string, ListingCategory>> GetByKeysAsync(
        IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    /// <summary>The whole tree, ordered for rendering.</summary>
    Task<IReadOnlyList<ListingCategory>> GetAllAsync(
        bool includeInactive, CancellationToken cancellationToken = default);

    /// <summary>Everything beneath a path, inclusive. The subtree filter's source.</summary>
    Task<IReadOnlyList<ListingCategory>> GetSubtreeAsync(
        string path, CancellationToken cancellationToken = default);

    Task<bool> HasChildrenAsync(string key, CancellationToken cancellationToken = default);

    Task AddAsync(ListingCategory category, CancellationToken cancellationToken = default);
}
