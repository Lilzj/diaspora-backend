using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>Soft-deletes a post the caller owns.</summary>
[Auditable]
[Authorize(Policy = CommunityPolicies.ManagePost)]
public sealed record DeletePostCommand : ICommand
{
    public required Guid PostId { get; init; }
}
