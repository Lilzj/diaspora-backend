using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Rsvps;

/// <summary>
/// Who is coming to an event.
/// <para>
/// Shown only where the organizer publishes the guest list, and to the organizer always. Who is
/// attending a support meeting or a political gathering is not a fact a link should carry.
/// </para>
/// </summary>
[Authorize]
public sealed record ListAttendeesQuery : IQuery<AttendeePageResponse>
{
    public required Guid EventId { get; init; }

    public IReadOnlyList<string>? Statuses { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
