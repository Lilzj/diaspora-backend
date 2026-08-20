using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// Resolves catalogue keys, and refuses the ones this deployment does not have.
/// <para>
/// <b>Every organization type and event category a write claims goes through here.</b> The
/// alternative — trusting the client's string — produces pages filed under categories that appear in
/// no picker and can never be found again, which is exactly what happens when a catalogue is
/// configurable and nothing checks it.
/// </para>
/// <para>
/// A retired entry resolves for an existing page and is refused for a new one: retiring a type must
/// not break the organizations already using it, and must stop new ones choosing it.
/// </para>
/// </summary>
public sealed class CommunityLifeCatalogResolver
{
    private readonly IOrganizationTypeRepository _types;
    private readonly IEventCategoryRepository _categories;

    public CommunityLifeCatalogResolver(
        IOrganizationTypeRepository types, IEventCategoryRepository categories)
    {
        _types = types;
        _categories = categories;
    }

    /// <summary>An organization type, or a failure naming the catalogue.</summary>
    public async Task<Result<OrganizationType>> RequireTypeAsync(
        string? key, bool allowRetired = false, CancellationToken cancellationToken = default)
    {
        var type = await _types.FindByKeyAsync(key, cancellationToken);

        if (type is null)
        {
            return Result.Failure<OrganizationType>(
                CommunityLifeErrors.OrganizationTypeNotFound);
        }

        return type.IsActive || allowRetired
            ? Result.Success(type)
            : Result.Failure<OrganizationType>(CommunityLifeErrors.OrganizationTypeInvalid);
    }

    /// <summary>An event category, or a failure naming the catalogue.</summary>
    public async Task<Result<EventCategory>> RequireCategoryAsync(
        string? key, bool allowRetired = false, CancellationToken cancellationToken = default)
    {
        var category = await _categories.FindByKeyAsync(key, cancellationToken);

        if (category is null)
        {
            return Result.Failure<EventCategory>(CommunityLifeErrors.EventCategoryNotFound);
        }

        return category.IsActive || allowRetired
            ? Result.Success(category)
            : Result.Failure<EventCategory>(CommunityLifeErrors.EventCategoryInvalid);
    }

    /// <summary>
    /// Every organization type by key, for rendering a page of cards.
    /// <para>
    /// One read for the whole page. Resolving a card's type name one at a time would be twenty round
    /// trips to render twenty names, and the catalogue is small enough that reading all of it is
    /// cheaper than reading the subset. Retired entries are included: a page still filed under a
    /// retired type must render with its name rather than blank.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<string, OrganizationType>> TypeLookupAsync(
        CancellationToken cancellationToken = default)
    {
        var types = await _types.ListAsync(includeRetired: true, cancellationToken);

        return types.ToDictionary(type => type.Key, StringComparer.Ordinal);
    }

    /// <summary>Every event category by key. See <see cref="TypeLookupAsync"/>.</summary>
    public async Task<IReadOnlyDictionary<string, EventCategory>> CategoryLookupAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = await _categories.ListAsync(includeRetired: true, cancellationToken);

        return categories.ToDictionary(category => category.Key, StringComparer.Ordinal);
    }

    /// <summary>
    /// The category keys behind a search facet.
    /// <para>
    /// Resolved once per query rather than joined into the event query, which keeps the hot path a
    /// single indexed predicate over an indexed column.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<string>> KeysForFacetAsync(
        string? facet, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facet))
        {
            return [];
        }

        var categories = await _categories.ListByFacetAsync(facet, cancellationToken);

        return [.. categories.Select(category => category.Key)];
    }

    /// <summary>The facet names in use across the live categories. What a filter bar renders.</summary>
    public async Task<IReadOnlyList<string>> FacetsAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = await _categories.ListAsync(
            includeRetired: false, cancellationToken);

        return
        [
            .. categories
                .SelectMany(category => category.Facets.Split(
                    ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
        ];
    }
}
