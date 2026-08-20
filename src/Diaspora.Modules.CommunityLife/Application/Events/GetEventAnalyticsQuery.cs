using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>What an organizer sees about how their event performed.</summary>
[Authorize(Permission = CommunityLifePermissions.ViewAnalytics)]
public sealed record GetEventAnalyticsQuery : IQuery<EventAnalyticsResponse>
{
    public required Guid EventId { get; init; }
}
