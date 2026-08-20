namespace Diaspora.SharedKernel.Pagination;

/// <summary>
/// Cursor-based pagination request for infinite scroll.
/// </summary>
public sealed record CursorPaginationRequest
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    public string? Cursor { get; init; }
    public int Limit { get; init; } = DefaultLimit;

    public CursorPaginationRequest Sanitize() => this with
    {
        Limit = Math.Clamp(Limit, 1, MaxLimit)
    };
}
