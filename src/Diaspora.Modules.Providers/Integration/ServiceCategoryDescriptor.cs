namespace Diaspora.Modules.Providers.Integration;

/// <summary>
/// One category, described in terms every consuming module can read.
/// <para>
/// Flat primitives, for the same reason <see cref="ProviderSummary"/> is: a consumer reading a
/// category should not have to reference <c>CategorySlug</c> and <c>CategoryPath</c> and
/// thereby take a dependency on how B7 stores a tree.
/// </para>
/// <para>
/// <see cref="Path"/> is exposed rather than hidden because it is the useful part: a consumer
/// that stores a category identifier can answer "is this inside Food &amp; Restaurants" with a
/// string comparison instead of a call back into B7 per row.
/// </para>
/// </summary>
public sealed record ServiceCategoryDescriptor
{
    public required Guid CategoryId { get; init; }

    public required string Slug { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>An emoji or icon name. Opaque — the client decides what it is.</summary>
    public string? Icon { get; init; }

    public Guid? ParentId { get; init; }

    /// <summary>The materialised path. A subtree test is a prefix comparison on this.</summary>
    public required string Path { get; init; }

    /// <summary>How deep. A root is 1.</summary>
    public required int Depth { get; init; }

    public required int SortOrder { get; init; }

    public required bool IsActive { get; init; }

    /// <summary>Active providers at or beneath this node. Approximate; refreshed by a sweeper.</summary>
    public required int ProviderCount { get; init; }
}
