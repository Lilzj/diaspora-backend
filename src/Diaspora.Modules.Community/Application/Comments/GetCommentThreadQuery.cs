using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Policies;

namespace Diaspora.Modules.Community.Application.Comments;

/// <summary>A page of a comment thread on any content.</summary>
[RequirePolicy(CommunityPolicies.Read)]
public sealed record GetCommentThreadQuery : IQuery<CommentThreadResponse>
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = CommunityLimits.DefaultFeedPageSize;
}
