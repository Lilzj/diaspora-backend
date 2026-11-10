using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>The revision history of a post. Serves the client's version screen.</summary>
[RequirePolicy(CommunityPolicies.Read)]
public sealed record ListPostRevisionsQuery : IQuery<IReadOnlyList<PostRevisionResponse>>
{
    public required Guid PostId { get; init; }
}
