namespace Diaspora.Modules.Providers.Extensibility.Categories;

/// <summary>
/// One category a contributor wants in the catalogue.
/// <para>
/// A flat record of primitives, deliberately. It crosses a contributor boundary, and a
/// contributor that had to construct <c>CategorySlug</c>, <c>CategoryPath</c> and
/// <c>ServiceCategoryTranslation</c> would be coupled to B7's domain internals rather than to
/// its contract — which is the coupling the seam exists to avoid. The seeder does the
/// validation and the construction.
/// </para>
/// <para>
/// <see cref="Aliases"/> is the field that makes migration free. B7's own seed registers every
/// legacy client string — the ten <c>mockCategories</c> names, the eighteen <c>OrgCategory</c>
/// members, the four free-text creator strings — as aliases on the node they belong to, so a
/// client that has not shipped a release keeps resolving.
/// </para>
/// </summary>
public sealed record ServiceCategorySeed
{
    /// <summary>Lower-case, hyphenated. The stable key, and the matching key for idempotence.</summary>
    public required string Slug { get; init; }

    /// <summary>The default-language display name.</summary>
    public required string Name { get; init; }

    /// <summary>The parent's slug, or null for a root. Resolved after every seed is collected.</summary>
    public string? ParentSlug { get; init; }

    /// <summary>An emoji or an icon name. B7 stores it opaquely; the client decides what it is.</summary>
    public string? Icon { get; init; }

    public string? Description { get; init; }

    /// <summary>Position in the filter row. Curated, because alphabetical is rarely right.</summary>
    public int SortOrder { get; init; }

    /// <summary>Other names this node answers to, including every legacy client spelling.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>Terms that should lead here without being names. "jollof", "braids", "visa".</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>Localized names, keyed by BCP-47 code.</summary>
    public IReadOnlyDictionary<string, string> Translations { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
