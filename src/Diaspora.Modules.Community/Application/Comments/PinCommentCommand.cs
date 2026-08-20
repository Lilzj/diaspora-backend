using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Community.Application.Comments;

/// <summary>Pins a comment to the top of its thread.</summary>
[Auditable]
public sealed record PinCommentCommand : ICommand
{
    public required Guid CommentId { get; init; }

    public required bool Pinned { get; init; }
}
