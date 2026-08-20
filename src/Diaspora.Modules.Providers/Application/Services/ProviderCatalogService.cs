using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Domain.Categories;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Integration;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// The read surface other modules use to work with service categories.
/// <para>
/// Published separately from <see cref="ProviderDirectoryService"/> because the consumers
/// differ. A vertical that stores a provider reference wants the directory; a vertical that
/// wants to categorise <em>its own</em> things — a marketplace item, a job, an event — wants the
/// catalogue and does not want providers at all. Marketplace filed under "Food &amp; Restaurants"
/// and Providers filed under "Food &amp; Restaurants" being the same node is what lets a
/// member's interest in one surface the other, and it costs those modules one interface rather
/// than a category tree each.
/// </para>
/// </summary>
public sealed class ProviderCatalogService : IProviderCatalog
{
    private readonly IServiceCategoryRepository _repository;
    private readonly ServiceCategoryResolver _resolver;

    public ProviderCatalogService(
        IServiceCategoryRepository repository, ServiceCategoryResolver resolver)
    {
        _repository = repository;
        _resolver = resolver;
    }

    public async Task<ServiceCategoryDescriptor?> FindAsync(
        ServiceCategoryId categoryId, CancellationToken cancellationToken = default)
    {
        var category = await _resolver.FindAsync(categoryId, cancellationToken);

        return category is null ? null : ServiceCategoryMapper.ToDescriptor(category);
    }

    public async Task<IReadOnlyDictionary<Guid, ServiceCategoryDescriptor>> FindManyAsync(
        IReadOnlyCollection<ServiceCategoryId> categoryIds,
        CancellationToken cancellationToken = default)
    {
        var found = await _resolver.LookupAsync(categoryIds, cancellationToken);

        return found.ToDictionary(
            entry => entry.Key, entry => ServiceCategoryMapper.ToDescriptor(entry.Value));
    }

    public async Task<ServiceCategoryDescriptor?> ResolveAsync(
        string term, CancellationToken cancellationToken = default)
    {
        var resolved = await _resolver.ResolveAsync(term, cancellationToken);

        return resolved.IsSuccess ? ServiceCategoryMapper.ToDescriptor(resolved.Value) : null;
    }

    public async Task<IReadOnlyList<ServiceCategoryDescriptor>> ListAsync(
        bool includeRetired = false, CancellationToken cancellationToken = default)
    {
        var categories = includeRetired
            ? await _repository.ListAllAsync(includeRetired: true, cancellationToken)
            : await _resolver.ListAsync(cancellationToken);

        return categories.Select(ServiceCategoryMapper.ToDescriptor).ToList();
    }

    public async Task<IReadOnlyList<ServiceCategoryDescriptor>> ListChildrenAsync(
        ServiceCategoryId parentId, CancellationToken cancellationToken = default)
    {
        var children = await _repository.ListChildrenAsync(parentId, cancellationToken);

        return children.Select(ServiceCategoryMapper.ToDescriptor).ToList();
    }

    /// <summary>
    /// The breadcrumb from the root to a node, inclusive.
    /// <para>
    /// Built by walking the node's own materialised path against the cached tree rather than by
    /// querying per level. The path already names every ancestor's slug; a query per level would
    /// be three round trips to recover information the node is carrying.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<ServiceCategoryDescriptor>> GetBreadcrumbAsync(
        ServiceCategoryId categoryId, CancellationToken cancellationToken = default)
    {
        var category = await _resolver.FindAsync(categoryId, cancellationToken);

        if (category is null)
        {
            return [];
        }

        var all = await _resolver.ListAsync(cancellationToken);
        var bySlug = all.ToDictionary(node => node.Slug.Value, StringComparer.Ordinal);

        var breadcrumb = new List<ServiceCategoryDescriptor>(category.Path.Depth);

        foreach (var slug in category.Path.Segments)
        {
            if (bySlug.TryGetValue(slug, out var node))
            {
                breadcrumb.Add(ServiceCategoryMapper.ToDescriptor(node));
            }
        }

        // The node itself is always the last entry, even when it has been retired and so is
        // absent from the cached active tree.
        if (breadcrumb.Count == 0 || breadcrumb[^1].CategoryId != category.Id.Value)
        {
            breadcrumb.Add(ServiceCategoryMapper.ToDescriptor(category));
        }

        return breadcrumb;
    }
}
