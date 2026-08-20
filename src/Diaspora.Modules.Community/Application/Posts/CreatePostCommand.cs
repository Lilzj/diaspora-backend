using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>Creates a post, as a draft or published.</summary>
[Auditable]
[Transactional]
public sealed record CreatePostCommand : ICommand<CommunityPostResponse>
{
    public required CreatePostRequest Request { get; init; }
}
