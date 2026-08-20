using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// A materialised path naming every ancestor of an area, root first — for example
/// <c>/GB/GB-LND/greater-london/london/peckham</c>.
/// <para>
/// This is the single design decision that makes the whole hierarchy cheap. Without it,
/// "every neighbourhood in the United Kingdom" is a recursive descent of unknown depth;
/// with it, the query is <c>WHERE path LIKE '/GB/%'</c> against a plain B-tree index, and
/// it costs the same whether the tree is four levels deep or eight. Ancestors are readable
/// straight off the row without a single join, which is what lets a search result render its
/// full "Peckham, London, United Kingdom" breadcrumb from one fetch.
/// </para>
/// <para>
/// The trade is that moving a subtree requires rewriting the paths beneath it. Administrative
/// boundaries are redrawn on a scale of decades, and a rewrite is a bounded background
/// operation; a recursive CTE on every discovery query is not.
/// </para>
/// </summary>
public sealed class HierarchyPath : ValueObject
{
    public const char Separator = '/';

    /// <summary>
    /// Eight levels of <see cref="Enums.AdministrativeLevel"/> at the slug limit, plus
    /// separators. Sized so the column can be indexed on every supported provider.
    /// </summary>
    public const int MaxLength = 1024;

    public string Value { get; }

    private HierarchyPath(string value)
    {
        Value = value;
    }

    /// <summary>The path of a root node — a country.</summary>
    public static HierarchyPath ForRoot(string segment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segment);
        return new HierarchyPath($"{Separator}{segment.Trim()}");
    }

    /// <summary>This path extended by one segment, for a child of the node it names.</summary>
    public Result<HierarchyPath> Append(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return Result.Failure<HierarchyPath>(Error.Validation(
                LocationErrorCodes.AreaNotFound, "A path segment is required."));
        }

        var candidate = $"{Value}{Separator}{segment.Trim()}";

        return candidate.Length > MaxLength
            ? Result.Failure<HierarchyPath>(Error.Validation(
                LocationErrorCodes.AreaNotFound,
                "The geographic hierarchy is too deep to address."))
            : Result.Success(new HierarchyPath(candidate));
    }

    public static Result<HierarchyPath> TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<HierarchyPath>(Error.Validation(
                LocationErrorCodes.AreaNotFound, "A hierarchy path is required."));
        }

        var trimmed = value.Trim();

        if (!trimmed.StartsWith(Separator))
        {
            return Result.Failure<HierarchyPath>(Error.Validation(
                LocationErrorCodes.AreaNotFound,
                "A hierarchy path must start at the root."));
        }

        return trimmed.Length > MaxLength
            ? Result.Failure<HierarchyPath>(Error.Validation(
                LocationErrorCodes.AreaNotFound, "The hierarchy path is too long."))
            : Result.Success(new HierarchyPath(trimmed));
    }

    /// <summary>The segments, root first.</summary>
    public IReadOnlyList<string> Segments =>
        Value.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>How many rungs below the root this node sits. A country is at depth 1.</summary>
    public int Depth => Segments.Count;

    /// <summary>The path of this node's parent, or <c>null</c> at the root.</summary>
    public HierarchyPath? Parent
    {
        get
        {
            var lastSeparator = Value.LastIndexOf(Separator);
            return lastSeparator <= 0 ? null : new HierarchyPath(Value[..lastSeparator]);
        }
    }

    /// <summary>The prefix a descendant's path must start with. Includes the trailing separator.</summary>
    public string DescendantPrefix => $"{Value}{Separator}";

    /// <summary>
    /// Whether <paramref name="other"/> sits anywhere beneath this node.
    /// <para>
    /// The trailing separator is what stops <c>/GB/london</c> from claiming
    /// <c>/GB/london-ontario</c> as a descendant — a prefix match without it is wrong in a
    /// way that only shows up once the catalogue has two similarly named places.
    /// </para>
    /// </summary>
    public bool Contains(HierarchyPath other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.Value.StartsWith(DescendantPrefix, StringComparison.Ordinal);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
