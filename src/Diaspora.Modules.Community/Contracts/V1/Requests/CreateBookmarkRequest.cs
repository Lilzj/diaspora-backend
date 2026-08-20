namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>Saving something.</summary>
public sealed record CreateBookmarkRequest
{
    /// <summary>A <c>CommunityContentType</c> name. The client's saved types all map.</summary>
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public Guid? FolderId { get; init; }

    /// <summary>Private to the member who saved it. Never projected to anybody else.</summary>
    public string? Note { get; init; }
}
