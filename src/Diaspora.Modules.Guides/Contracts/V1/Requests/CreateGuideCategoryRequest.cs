namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Adds a category to the catalogue. The key is the contract — it is what a contributor seed
/// declares, what a URL carries and what guides store — and it can never be changed after.
/// </summary>
public sealed record CreateGuideCategoryRequest
{
    /// <summary>Lower-case kebab. Stable forever.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>An icon name the client resolves. Stored opaquely.</summary>
    public string? Icon { get; init; }

    public int Order { get; init; }

    /// <summary>The parent category's key, for a shallow hierarchy.</summary>
    public string? ParentKey { get; init; }
}
