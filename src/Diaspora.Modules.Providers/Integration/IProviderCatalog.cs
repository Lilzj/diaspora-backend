using Diaspora.Modules.Providers.Domain.Identifiers;

namespace Diaspora.Modules.Providers.Integration;

/// <summary>
/// The read surface other modules use to work with service categories.
/// <para>
/// Published separately from <see cref="IProviderDirectory"/> because the consumers differ. A
/// vertical that stores a provider reference needs the directory; a vertical that wants to
/// categorise <em>its own</em> things — a marketplace item, a job, an event — needs the
/// catalogue and does not need providers at all. Marketplace filed under "Food &amp; Restaurants"
/// and Providers filed under "Food &amp; Restaurants" being the same node is what lets a member's
/// interest in one surface the other, and it costs those modules one interface rather than a
/// category tree each.
/// </para>
/// <para>
/// <see cref="ResolveAsync"/> is the method that pays for the whole design: it matches on name,
/// slug or any registered alias, so a consumer holding a legacy string — the frontend's
/// <c>OrgCategory</c> values, an imported dataset's labels — resolves without a migration.
/// </para>
/// </summary>
public interface IProviderCatalog
{
    /// <summary>One category, or null.</summary>
    Task<ServiceCategoryDescriptor?> FindAsync(
        ServiceCategoryId categoryId, CancellationToken cancellationToken = default);

    /// <summary>Several at once. The N+1 guard for a list of categorised things.</summary>
    Task<IReadOnlyDictionary<Guid, ServiceCategoryDescriptor>> FindManyAsync(
        IReadOnlyCollection<ServiceCategoryId> categoryIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a caller's term through name, slug or alias.
    /// <para>
    /// The migration path: <c>Restaurants</c>, <c>Food &amp; Restaurants</c>,
    /// <c>food-restaurants</c> and <c>Food and Restaurants</c> all land on the same node.
    /// </para>
    /// </summary>
    Task<ServiceCategoryDescriptor?> ResolveAsync(
        string term, CancellationToken cancellationToken = default);

    /// <summary>The whole active tree, root-first and sorted. What a filter row renders from.</summary>
    Task<IReadOnlyList<ServiceCategoryDescriptor>> ListAsync(
        bool includeRetired = false, CancellationToken cancellationToken = default);

    /// <summary>A node's direct children.</summary>
    Task<IReadOnlyList<ServiceCategoryDescriptor>> ListChildrenAsync(
        ServiceCategoryId parentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The breadcrumb from the root to a node, inclusive. What a listing card renders as
    /// "Food &amp; Restaurants › Restaurants".
    /// </summary>
    Task<IReadOnlyList<ServiceCategoryDescriptor>> GetBreadcrumbAsync(
        ServiceCategoryId categoryId, CancellationToken cancellationToken = default);
}
