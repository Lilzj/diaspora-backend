namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Adds a node to the commerce taxonomy.
/// <para>
/// The administrative counterpart to <c>IListingCategoryContributor</c>: a contributor ships a
/// taxonomy with the code, and this is how an operator adds the one their market needs without
/// waiting for a release.
/// </para>
/// </summary>
public sealed record CreateCategoryRequest
{
    /// <summary>Lower-case and hyphenated. Normalised server-side; must be unique.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>The parent's key. Null puts the node at the top. Nesting stops at three levels.</summary>
    public string? ParentKey { get; init; }

    /// <summary>An icon name the client resolves. The platform ships no images.</summary>
    public string? IconKey { get; init; }

    public int Order { get; init; }
}
