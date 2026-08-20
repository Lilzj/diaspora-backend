namespace Diaspora.Modules.Guides.Extensibility.Categories;

/// <summary>
/// One category a contributor wants in the catalogue.
/// <para>
/// A flat record of primitives, deliberately. It crosses a contributor boundary, and a
/// contributor that had to construct this module's aggregate would be coupled to B8's domain
/// internals rather than to its contract. The seeder does the validation and the construction.
/// </para>
/// </summary>
public sealed record GuideCategorySeed
{
    /// <summary>Lower-case kebab. The stable key, and the matching key for idempotence.</summary>
    public required string Key { get; init; }

    /// <summary>The default-language display name.</summary>
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>An emoji or an icon name. B8 stores it opaquely; the client decides what it is.</summary>
    public string? Icon { get; init; }

    /// <summary>Position in the picker. Curated, because alphabetical is rarely right.</summary>
    public int Order { get; init; }

    /// <summary>The parent's key, or null for a root. Shallow hierarchies only.</summary>
    public string? ParentKey { get; init; }
}
