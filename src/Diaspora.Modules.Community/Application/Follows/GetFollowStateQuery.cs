using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Follows;

/// <summary>
/// Whether the caller follows a target, with the target's follower count.
/// <para>
/// Added 20 Oct 2026. The follow and unfollow routes answered with the state only after a press, so
/// a Follow button had no way to know it should already say "Following".
/// </para>
/// </summary>
public sealed record GetFollowStateQuery : IQuery<FollowStateResponse>
{
    public required Guid TargetId { get; init; }

    public string? TargetType { get; init; }
}
