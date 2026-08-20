namespace Diaspora.Modules.Jobs.Extensibility.Categories;

/// <summary>
/// One employment domain as a contributor declares it.
/// <para>
/// A flat record rather than the aggregate, so a contributor never constructs a domain object
/// and never has to know how one is validated. The seeder turns these into <c>JobCategory</c>
/// rows and rejects the ones that do not survive validation, logging rather than failing
/// startup — a bad category from one contributor must not stop the platform booting.
/// </para>
/// </summary>
public sealed record JobCategorySeed
{
    /// <summary>Lower-case, hyphenated. The stable handle postings store.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>A parent's key, for one level of nesting. Null at the top.</summary>
    public string? ParentKey { get; init; }

    /// <summary>An icon name the client resolves. The platform ships no images.</summary>
    public string? IconKey { get; init; }

    /// <summary>Display order within its level.</summary>
    public int Order { get; init; }
}
