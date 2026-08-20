using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Categories;
using Diaspora.Modules.Providers.Integration;

namespace Diaspora.Modules.Providers.Application.Mapping;

/// <summary>
/// Turns a category into the shapes the wire and other modules read.
/// <para>
/// Localisation happens here and nowhere else. <see cref="ServiceCategory.NameIn"/> falls back
/// on the primary subtag, so a client asking for <c>pt-BR</c> gets the Portuguese translation
/// rather than English — which is the difference between a francophone Ivorian reading
/// "Finance &amp; Legal" and reading something they understand.
/// </para>
/// </summary>
public static class ServiceCategoryMapper
{
    public static ServiceCategoryResponse ToResponse(
        ServiceCategory category, string? languageCode = null)
    {
        ArgumentNullException.ThrowIfNull(category);

        return new ServiceCategoryResponse
        {
            CategoryId = category.Id.Value,
            Slug = category.Slug.Value,
            Name = category.NameIn(languageCode),
            Description = category.Description,
            Icon = category.Icon,
            ParentId = category.ParentId?.Value,
            Path = category.Path.Value,
            Depth = category.Path.Depth,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive,
            ProviderCount = category.ProviderCount
        };
    }

    /// <summary>The thinner shape a provider card embeds.</summary>
    public static ProviderCategoryResponse ToSummary(
        ServiceCategory category, string? languageCode = null)
    {
        ArgumentNullException.ThrowIfNull(category);

        return new ProviderCategoryResponse
        {
            CategoryId = category.Id.Value,
            Slug = category.Slug.Value,
            Name = category.NameIn(languageCode),
            Icon = category.Icon,
            ParentId = category.ParentId?.Value,
            Path = category.Path.Value
        };
    }

    /// <summary>
    /// The placeholder a card falls back to when its category could not be resolved.
    /// <para>
    /// Returning a placeholder rather than failing the whole read is deliberate: a category
    /// retired without its providers being moved is a data problem, and answering a discovery
    /// request with a 500 because of it would take the whole directory down over one bad row.
    /// </para>
    /// </summary>
    public static ProviderCategoryResponse Unknown(Guid categoryId) => new()
    {
        CategoryId = categoryId,
        Slug = "uncategorised",
        Name = "Uncategorised"
    };

    /// <summary>The cross-module shape, for consumers that must not reference B7's domain.</summary>
    public static ServiceCategoryDescriptor ToDescriptor(ServiceCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return new ServiceCategoryDescriptor
        {
            CategoryId = category.Id.Value,
            Slug = category.Slug.Value,
            Name = category.Name,
            Description = category.Description,
            Icon = category.Icon,
            ParentId = category.ParentId?.Value,
            Path = category.Path.Value,
            Depth = category.Path.Depth,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive,
            ProviderCount = category.ProviderCount
        };
    }
}
