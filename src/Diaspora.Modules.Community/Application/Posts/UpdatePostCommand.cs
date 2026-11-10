using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>Edits a post the caller owns. Captures a revision first.</summary>
[Auditable]
[Transactional]
[Authorize(Policy = CommunityPolicies.ManagePost)]
public sealed record UpdatePostCommand : ICommand<CommunityPostResponse>
{
    public required Guid PostId { get; init; }

    public required UpdatePostRequest Request { get; init; }
}
