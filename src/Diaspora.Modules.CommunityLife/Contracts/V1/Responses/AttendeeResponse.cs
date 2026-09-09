namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>An attendee, as the organizer's list renders one.</summary>
public sealed record AttendeeResponse
{
    public required Guid RsvpId { get; init; }

    public required Guid AttendeeUserId { get; init; }

    /// <summary>
    /// What to call them.
    /// <para>
    /// <b>The list had no names on it.</b> An attendee came back as an identifier, a status and a
    /// party size, so an organizer checking people in at a door was reading rows of nothing —
    /// and the screen that renders this could only show blanks.
    /// </para>
    /// <para>
    /// Resolved through the kernel's member port, the same one that named the people in a
    /// conversation. Null when the member cannot be resolved, which a caller should render as
    /// absent rather than as somebody called "Unknown".
    /// </para>
    /// </summary>
    public string? DisplayName { get; init; }

    public required string Status { get; init; }

    public required int PartySize { get; init; }

    /// <summary>What they told the organizer. Visible to organizers only.</summary>
    public string? Note { get; init; }

    public required DateTimeOffset RespondedAt { get; init; }

    public DateTimeOffset? CheckedInAt { get; init; }
}
