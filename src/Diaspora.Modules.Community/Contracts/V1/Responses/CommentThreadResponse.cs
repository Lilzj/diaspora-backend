namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>A page of a thread.</summary>
public sealed record CommentThreadResponse
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required IReadOnlyList<CommentResponse> Comments { get; init; }

    public required int TotalCount { get; init; }

    /// <summary>Null on the last page.</summary>
    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    public required bool CommentsClosed { get; init; }
}
