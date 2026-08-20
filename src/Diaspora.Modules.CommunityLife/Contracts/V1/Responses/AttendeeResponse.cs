namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>An attendee, as the organizer's list renders one.</summary>
public sealed record AttendeeResponse
{
    public required Guid RsvpId { get; init; }

    public required Guid AttendeeUserId { get; init; }

    public required string Status { get; init; }

    public required int PartySize { get; init; }

    /// <summary>What they told the organizer. Visible to organizers only.</summary>
    public string? Note { get; init; }

    public required DateTimeOffset RespondedAt { get; init; }

    public DateTimeOffset? CheckedInAt { get; init; }
}
