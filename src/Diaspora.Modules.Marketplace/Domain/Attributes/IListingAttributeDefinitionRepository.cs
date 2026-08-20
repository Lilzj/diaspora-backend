using Diaspora.Modules.Marketplace.Domain.Identifiers;

namespace Diaspora.Modules.Marketplace.Domain.Attributes;

/// <summary>How the application layer reaches category attribute definitions.</summary>
public interface IListingAttributeDefinitionRepository
{
    Task<ListingAttributeDefinition?> GetAsync(
        ListingAttributeDefinitionId id, CancellationToken cancellationToken = default);

    Task<ListingAttributeDefinition?> GetByKeyAsync(
        string key, string categoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every definition that applies to a category, ancestors included.
    /// <para>
    /// The read the create form and the publish check both make. Ancestors included because a
    /// definition on <c>/electronics/</c> applies to every phone beneath it — declaring "brand"
    /// once is the whole point of scoping by path.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ListingAttributeDefinition>> GetForCategoryPathAsync(
        string categoryPath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingAttributeDefinition>> GetAllAsync(
        bool includeInactive, CancellationToken cancellationToken = default);

    Task AddAsync(
        ListingAttributeDefinition definition, CancellationToken cancellationToken = default);
}
