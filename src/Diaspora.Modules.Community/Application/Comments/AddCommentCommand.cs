using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Comments;

/// <summary>Adds a comment or a reply to any addressable content.</summary>
[Auditable]
[Transactional]
[Authorize(Policy = CommunityPolicies.Comment)]
public sealed record AddCommentCommand : ICommand<CommentResponse>
{
    /// <summary>From the route. The body may name the content instead.</summary>
    public string? ContentType { get; init; }

    public Guid? ContentId { get; init; }

    public required CreateCommentRequest Request { get; init; }
}
