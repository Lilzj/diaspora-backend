using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Comments;

/// <summary>Edits a comment the caller owns.</summary>
[Auditable]
[RequirePolicy(CommunityPolicies.ManageComment)]
public sealed record UpdateCommentCommand : ICommand<CommentResponse>
{
    public required Guid CommentId { get; init; }

    public required UpdateCommentRequest Request { get; init; }
}
