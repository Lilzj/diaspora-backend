using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Community.Application.Bookmarks;

/// <summary>Unsaves content.</summary>
[Auditable]
public sealed record RemoveBookmarkCommand : ICommand
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }
}
