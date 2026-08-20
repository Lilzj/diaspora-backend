using Diaspora.Modules.Guides.Domain.Categories;
using Diaspora.Modules.Guides.Integration;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// The read surface other modules use to work with the guide category catalogue — the
/// implementation of <see cref="IGuideCatalog"/>, thin by design: the resolver owns the
/// caching and the repository owns the query, and this class owns only the translation to the
/// published descriptor shape.
/// </summary>
public sealed class GuideCatalogService : IGuideCatalog
{
    private readonly GuideCategoryResolver _categories;

    public GuideCatalogService(GuideCategoryResolver categories) => _categories = categories;

    public async Task<GuideCategoryDescriptor?> FindCategoryAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var category = await _categories.FindAsync(key, cancellationToken);

        return category is null ? null : ToDescriptor(category);
    }

    public async Task<IReadOnlyList<GuideCategoryDescriptor>> ListCategoriesAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var categories = await _categories.ListAsync(includeInactive, cancellationToken);

        return categories.Select(ToDescriptor).ToList();
    }

    private static GuideCategoryDescriptor ToDescriptor(GuideCategory category) => new()
    {
        Key = category.Key,
        Name = category.Name,
        Description = category.Description,
        Icon = category.Icon,
        Order = category.Order,
        IsActive = category.IsActive,
        ParentKey = category.ParentKey
    };
}
