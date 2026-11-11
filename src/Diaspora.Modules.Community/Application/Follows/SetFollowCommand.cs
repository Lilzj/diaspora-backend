using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Follows;

/// <summary>Follows or unfollows a member, an organization or a hashtag.</summary>
[Auditable]
[RequirePolicy(CommunityPolicies.Follow)]
public sealed record SetFollowCommand : ICommand<FollowResponse>
{
    public required Guid TargetId { get; init; }

    public string? TargetType { get; init; }

    public required bool Follow { get; init; }

    public bool Muted { get; init; }
}
