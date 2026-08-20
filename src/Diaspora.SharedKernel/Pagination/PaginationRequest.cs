namespace Diaspora.SharedKernel.Pagination;

/// <summary>
/// Pagination request parameters for offset-based pagination.
/// </summary>
public sealed record PaginationRequest
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    public int Page { get; init; } = DefaultPage;
    public int PageSize { get; init; } = DefaultPageSize;

    public PaginationRequest Sanitize() => this with
    {
        Page = Math.Max(1, Page),
        PageSize = Math.Clamp(PageSize, 1, MaxPageSize)
    };

    public int Skip => (Page - 1) * PageSize;
}
