using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>Reads one post, if the caller is allowed to see it.</summary>
[Authorize(Policy = CommunityPolicies.Read)]
public sealed record GetPostQuery : IQuery<CommunityPostResponse>
{
    public required Guid PostId { get; init; }
}
