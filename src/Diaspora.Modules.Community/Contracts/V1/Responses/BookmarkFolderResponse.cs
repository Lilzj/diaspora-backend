namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>A member-named folder.</summary>
public sealed record BookmarkFolderResponse
{
    public required Guid FolderId { get; init; }

    public required string Name { get; init; }

    public string? Glyph { get; init; }

    public required int ItemCount { get; init; }

    public required int Position { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
