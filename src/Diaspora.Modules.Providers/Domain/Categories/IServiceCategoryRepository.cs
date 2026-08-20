using Diaspora.Modules.Providers.Domain.Identifiers;

namespace Diaspora.Modules.Providers.Domain.Categories;

/// <summary>
/// Reads and writes the service category catalogue.
/// <para>
/// The catalogue is small — a few hundred rows at most — read on nearly every request, and
/// changed a handful of times a year. That shape is what <see cref="ListAllAsync"/> is for: the
/// whole tree in one query, cached per request, rather than a lookup per provider card. The
/// alternative, resolving each card's category individually, is twenty round trips to render a
/// discovery page.
/// </para>
/// </summary>
public interface IServiceCategoryRepository
{
    Task<ServiceCategory?> GetAsync(
        ServiceCategoryId id, CancellationToken cancellationToken = default);

    Task<ServiceCategory?> GetBySlugAsync(
        CategorySlug slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceCategory>> GetManyAsync(
        IReadOnlyCollection<ServiceCategoryId> ids, CancellationToken cancellationToken = default);

    /// <summary>The whole tree, aliases and translations included. Cached per request.</summary>
    Task<IReadOnlyList<ServiceCategory>> ListAllAsync(
        bool includeRetired = false, CancellationToken cancellationToken = default);

    /// <summary>A node's direct children.</summary>
    Task<IReadOnlyList<ServiceCategory>> ListChildrenAsync(
        ServiceCategoryId parentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A node and everything beneath it, by prefix match on the materialised path. What a
    /// category move loads so it can rewrite every descendant's path in the same transaction.
    /// </summary>
    Task<IReadOnlyList<ServiceCategory>> ListSubtreeAsync(
        CategoryPath path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a caller's term to a category through its name, slug or any alias.
    /// <para>
    /// The migration path the frontend contract report's D-3 depends on: a client still sending
    /// <c>category=Restaurants</c> resolves to the <c>restaurants</c> node without a release.
    /// </para>
    /// </summary>
    Task<ServiceCategory?> ResolveAsync(string term, CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(CategorySlug slug, CancellationToken cancellationToken = default);

    /// <summary>Whether any provider currently sits under a node. Guards a retirement.</summary>
    Task<bool> HasProvidersAsync(
        ServiceCategoryId id, CancellationToken cancellationToken = default);

    Task AddAsync(ServiceCategory category, CancellationToken cancellationToken = default);
}
