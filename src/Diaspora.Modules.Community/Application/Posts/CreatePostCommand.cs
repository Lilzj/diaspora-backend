using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>Creates a post, as a draft or published.</summary>
[Auditable]
[Transactional]
[Diaspora.Application.Behaviors.PostsInArea(Diaspora.Contracts.Moderation.V1.ModerationAreas.Community)]
[RequirePolicy(CommunityPolicies.Post)]
public sealed record CreatePostCommand : ICommand<CommunityPostResponse>
{
    public required CreatePostRequest Request { get; init; }
}
