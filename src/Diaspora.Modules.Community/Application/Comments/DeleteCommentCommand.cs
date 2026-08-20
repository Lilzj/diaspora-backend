using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Community.Application.Comments;

/// <summary>Soft-deletes a comment the caller owns. The row is tombstoned, not removed.</summary>
[Auditable]
public sealed record DeleteCommentCommand : ICommand
{
    public required Guid CommentId { get; init; }
}
