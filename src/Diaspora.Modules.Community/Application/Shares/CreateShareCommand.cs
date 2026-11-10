using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Shares;

/// <summary>Records a share.</summary>
[Auditable]
[Authorize(Policy = CommunityPolicies.Share)]
public sealed record CreateShareCommand : ICommand<ShareResponse>
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required CreateShareRequest Request { get; init; }
}
