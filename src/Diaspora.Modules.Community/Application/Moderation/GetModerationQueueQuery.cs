using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Policies;

namespace Diaspora.Modules.Community.Application.Moderation;

/// <summary>The moderator queue: open reports, grouped by the content they are about.</summary>
[Authorize(Policy = CommunityPolicies.ModerationRead)]
public sealed record GetModerationQueueQuery : IQuery<IReadOnlyList<ModerationQueueItemResponse>>
{
    public string? ContentType { get; init; }

    public string? Reason { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = CommunityLimits.DefaultFeedPageSize;
}
