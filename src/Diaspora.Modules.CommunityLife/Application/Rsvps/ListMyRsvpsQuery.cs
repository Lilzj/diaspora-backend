using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Rsvps;

/// <summary>What the caller has said they are going to.</summary>
[Authorize]
public sealed record ListMyRsvpsQuery : IQuery<RsvpPageResponse>
{
    public bool UpcomingOnly { get; init; } = true;

    public IReadOnlyList<string>? Statuses { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
