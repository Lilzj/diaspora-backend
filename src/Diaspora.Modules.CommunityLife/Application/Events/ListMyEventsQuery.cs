using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>The caller's own events, drafts included.</summary>
[Authorize]
public sealed record ListMyEventsQuery : IQuery<EventSearchResultResponse>
{
    public Guid? OrganizationId { get; init; }

    public IReadOnlyList<string>? Statuses { get; init; }

    public bool IncludePast { get; init; } = true;

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
