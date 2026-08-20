namespace Diaspora.SharedKernel.Pagination;

/// <summary>
/// Paginated result with offset-based pagination metadata.
/// </summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages { get; }
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;

    private PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount) =>
        new(items, page, pageSize, totalCount);

    public static PagedResult<T> Empty(int page, int pageSize) =>
        new([], page, pageSize, 0);

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> mapper) =>
        PagedResult<TOut>.Create(
            Items.Select(mapper).ToList().AsReadOnly(),
            Page,
            PageSize,
            TotalCount);
}
