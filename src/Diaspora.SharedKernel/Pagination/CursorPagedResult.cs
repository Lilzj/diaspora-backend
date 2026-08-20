namespace Diaspora.SharedKernel.Pagination;

/// <summary>
/// Cursor-based paginated result for infinite scroll scenarios.
/// </summary>
public sealed class CursorPagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public string? NextCursor { get; }
    public bool HasMore { get; }
    public int? TotalCount { get; }

    private CursorPagedResult(IReadOnlyList<T> items, string? nextCursor, bool hasMore, int? totalCount = null)
    {
        Items = items;
        NextCursor = nextCursor;
        HasMore = hasMore;
        TotalCount = totalCount;
    }

    public static CursorPagedResult<T> Create(IReadOnlyList<T> items, string? nextCursor, bool hasMore, int? totalCount = null) =>
        new(items, nextCursor, hasMore, totalCount);

    public static CursorPagedResult<T> Empty() =>
        new([], null, false, 0);
}
