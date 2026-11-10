using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>How an author closes a post that is in effect.</summary>
public enum PostClosure
{
    /// <summary>The report was wrong.</summary>
    Withdraw,

    /// <summary>What was reported is over or will not happen.</summary>
    Cancel
}

/// <summary>Withdraws or cancels a post the caller wrote.</summary>
[Auditable]
[Transactional]
[RequirePolicy(CommunityPolicies.ManagePost)]
public sealed record ClosePostCommand : ICommand<CommunityPostResponse>
{
    public required Guid PostId { get; init; }

    public required PostClosure Closure { get; init; }
}
