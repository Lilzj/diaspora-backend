using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Categories;

/// <summary>
/// A category's position in the tree, as a materialised path.
/// <para>
/// <b>The same technique B4 uses for administrative areas, adopted for the same reason.</b>
/// "Every provider in Food &amp; Restaurants, including its subcategories" is the single most
/// common category query a directory runs, and the three ways to answer it are a recursive CTE
/// (correct, slow, and different on every provider), a closure table (correct, fast, and three
/// times the write cost) or a prefix match on a stored path (correct, fast, one index). B4 chose
/// the third and proved it; B7 does not re-litigate it.
/// </para>
/// <para>
/// The path is built from slugs rather than identifiers so it is readable in a database client
/// and in a log — <c>/food-restaurants/restaurants/</c> rather than thirty-two hex characters
/// twice. Both ends carry the separator so a prefix match cannot produce a false positive:
/// without the leading and trailing slashes, <c>/food</c> would match <c>/food-hygiene</c>.
/// </para>
/// </summary>
public sealed class CategoryPath : ValueObject
{
    public const char Separator = '/';

    public const int MaxLength = 512;

    private CategoryPath(string value, int depth)
    {
        Value = value;
        Depth = depth;
    }

    private CategoryPath() { }

    /// <summary>Always begins and ends with <see cref="Separator"/>.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>How many levels down. A root is 1.</summary>
    public int Depth { get; private set; }

    /// <summary>The path of a top-level category.</summary>
    public static CategoryPath Root(CategorySlug slug)
    {
        ArgumentNullException.ThrowIfNull(slug);

        return new CategoryPath($"{Separator}{slug.Value}{Separator}", 1);
    }

    /// <summary>The path of a category beneath another.</summary>
    public static CategoryPath Below(CategoryPath parent, CategorySlug slug)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(slug);

        return new CategoryPath($"{parent.Value}{slug.Value}{Separator}", parent.Depth + 1);
    }

    /// <summary>Rebuilds a path from storage without revalidating it.</summary>
    public static CategoryPath FromStored(string value, int depth) => new(value, depth);

    /// <summary>Whether this category is at or beneath another. The subtree query, in memory.</summary>
    public bool IsWithin(CategoryPath other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Value.StartsWith(other.Value, StringComparison.Ordinal);
    }

    /// <summary>The prefix a subtree query filters on: <c>path LIKE prefix || '%'</c>.</summary>
    public string SubtreePrefix => Value;

    /// <summary>The slugs from the root to this node, for rendering a breadcrumb.</summary>
    public IReadOnlyList<string> Segments =>
        Value.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
