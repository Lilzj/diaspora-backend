using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>Soft-deletes a post the caller owns.</summary>
[Auditable]
public sealed record DeletePostCommand : ICommand
{
    public required Guid PostId { get; init; }
}
