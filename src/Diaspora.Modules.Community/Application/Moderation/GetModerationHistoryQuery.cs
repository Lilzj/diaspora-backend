using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Moderation;

/// <summary>The append-only decision history for one piece of content.</summary>
[Authorize(Policy = CommunityPolicies.ModerationRead)]
public sealed record GetModerationHistoryQuery : IQuery<IReadOnlyList<ModerationDecisionResponse>>
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }
}
