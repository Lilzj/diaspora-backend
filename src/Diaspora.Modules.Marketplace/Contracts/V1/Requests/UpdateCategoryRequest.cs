namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Renames or retires a category.
/// <para>
/// <b>The key never moves.</b> Listings store it, so changing it would orphan every listing filed
/// under the node. Retiring takes the node off the create form and leaves a decade of listings
/// readable, which is the only safe way to remove a category from a marketplace that has been
/// running.
/// </para>
/// </summary>
public sealed record UpdateCategoryRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? IconKey { get; init; }

    public int Order { get; init; }

    /// <summary>Whether new listings may still be filed here.</summary>
    public bool IsActive { get; init; } = true;
}
