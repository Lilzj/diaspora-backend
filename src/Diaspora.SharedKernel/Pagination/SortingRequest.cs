namespace Diaspora.SharedKernel.Pagination;

/// <summary>
/// Sorting parameters.
/// </summary>
public sealed record SortingRequest
{
    public string? SortBy { get; init; }
    public SortDirection Direction { get; init; } = SortDirection.Ascending;
}
