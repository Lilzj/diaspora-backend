using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>One event, by identifier or by slug. Anonymous.</summary>
public sealed record GetEventQuery : IQuery<EventResponse>
{
    public Guid? EventId { get; init; }

    public string? Slug { get; init; }
}
