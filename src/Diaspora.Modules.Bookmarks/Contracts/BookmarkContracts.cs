namespace Diaspora.Modules.Bookmarks.Contracts;

/// <summary>Request to save something.</summary>
public sealed record CreateBookmarkRequest
{
    public required string TargetType { get; init; }

    public required string TargetId { get; init; }

    public string? Note { get; init; }
}

/// <summary>One saved item.</summary>
public sealed record BookmarkResponse
{
    public required Guid Id { get; init; }

    public required string TargetType { get; init; }

    public required string TargetId { get; init; }

    public string? Note { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
