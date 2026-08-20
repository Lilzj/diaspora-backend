namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>One member's response to one event.</summary>
public sealed record RsvpResponse
{
    public required Guid RsvpId { get; init; }

    public required Guid EventId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required Guid AttendeeUserId { get; init; }

    public required string Status { get; init; }

    public required int PartySize { get; init; }

    public string? Note { get; init; }

    /// <summary>Why it ended, when it did. Never <c>None</c> on a terminal response.</summary>
    public required string DeclineReason { get; init; }

    public required DateTimeOffset RespondedAt { get; init; }

    public DateTimeOffset? CheckedInAt { get; init; }

    public string? CheckInMethod { get; init; }

    public required int Revision { get; init; }

    // ── B12.1. Appended, never inserted — see EventResponse. ────────────────────────────────

    /// <summary>
    /// Where in the queue a waitlisted response sits, counting from one. Null unless the status is
    /// <c>Waitlisted</c>.
    /// <para>
    /// <b>Computed by the server, and it has to be.</b> "You are fourth in the queue" is a fact
    /// about every other response to that event, and a client that tried to derive it would need the
    /// whole guest list — which is exactly the thing an attendee is not allowed to read.
    /// </para>
    /// </summary>
    public int? WaitlistPosition { get; init; }

    /// <summary>The repeating event this date belongs to, when it belongs to one.</summary>
    public Guid? SeriesId { get; init; }
}
