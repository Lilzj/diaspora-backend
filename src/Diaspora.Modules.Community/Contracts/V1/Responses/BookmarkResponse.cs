namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>A saved item.</summary>
public sealed record BookmarkResponse
{
    public required Guid BookmarkId { get; init; }

    /// <summary>The client uses this spelling in its saved types.</summary>
    public required Guid SavedId { get; init; }

    public required Guid UserId { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public Guid? FolderId { get; init; }

    /// <summary>Private to the owner. Never present on anybody else's projection.</summary>
    public string? Note { get; init; }

    /// <summary>Present when the owning module could describe the item.</summary>
    public string? Title { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
