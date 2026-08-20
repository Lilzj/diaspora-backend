namespace Diaspora.Modules.Marketplace.Extensibility.Categories;

/// <summary>
/// One category as a contributor declares it.
/// <para>
/// A flat record rather than the aggregate, so a contributor never constructs a domain object and
/// never has to know how one is validated. The seeder turns these into <c>ListingCategory</c> rows
/// and drops the ones that do not survive validation, logging rather than failing startup — a bad
/// category from one contributor must not stop the platform booting.
/// </para>
/// </summary>
public sealed record ListingCategorySeed
{
    /// <summary>Lower-case, hyphenated. The stable handle listings store.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>A parent's key. Null at the top. Nesting is bounded at three levels.</summary>
    public string? ParentKey { get; init; }

    /// <summary>An icon name the client resolves. The platform ships no images.</summary>
    public string? IconKey { get; init; }

    /// <summary>Display order within its level.</summary>
    public int Order { get; init; }
}
