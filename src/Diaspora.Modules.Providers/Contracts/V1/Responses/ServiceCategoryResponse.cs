namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// One node in the service category catalogue.
/// <para>
/// Returned flat with a <see cref="Path"/> rather than nested, so a client can render the filter
/// row without walking a tree and can build one when it wants to. The list comes back ordered by
/// path, which makes it already depth-first.
/// </para>
/// <para>
/// <see cref="ProviderCount"/> lets the client render "Food &amp; Restaurants (128)". Approximate
/// and refreshed by a sweeper: a headline number an hour stale is a far better trade than a
/// browse screen that issues an aggregate query per chip.
/// </para>
/// </summary>
public sealed record ServiceCategoryResponse
{
    public required Guid CategoryId { get; init; }

    public required string Slug { get; init; }

    /// <summary>Localised when the caller asked for a language B7 has a translation in.</summary>
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>An emoji or an icon name. Opaque.</summary>
    public string? Icon { get; init; }

    public Guid? ParentId { get; init; }

    /// <summary>The materialised path. A client can test containment without another call.</summary>
    public required string Path { get; init; }

    /// <summary>How deep. A root is 1.</summary>
    public required int Depth { get; init; }

    public required int SortOrder { get; init; }

    public required bool IsActive { get; init; }

    /// <summary>Active providers at or beneath this node. Approximate.</summary>
    public required int ProviderCount { get; init; }
}
