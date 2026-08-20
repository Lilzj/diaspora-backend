namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>Moving a saved item between folders. A null folder means the default collection.</summary>
public sealed record MoveBookmarkRequest
{
    public Guid? FolderId { get; init; }
}
