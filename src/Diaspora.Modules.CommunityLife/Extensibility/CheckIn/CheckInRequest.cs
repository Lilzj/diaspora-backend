using Diaspora.Modules.CommunityLife.Domain.Enums;

namespace Diaspora.Modules.CommunityLife.Extensibility.CheckIn;

/// <summary>
/// Somebody arriving at a door.
/// <para>
/// <b><see cref="Token"/> is why this type exists.</b> A manual check-in identifies the attendee by
/// identifier; a scanned one identifies them by whatever was on the badge. Carrying an opaque token
/// alongside the identifier is what lets a QR method land later without changing the request shape,
/// the aggregate, or anything above them.
/// </para>
/// </summary>
public sealed record CheckInRequest
{
    public required Guid EventId { get; init; }

    /// <summary>The attendee, when the caller knows who they are. Null for a token-only arrival.</summary>
    public Guid? AttendeeUserId { get; init; }

    /// <summary>The response being checked in, when the caller has it.</summary>
    public Guid? RsvpId { get; init; }

    /// <summary>
    /// Whatever the door presented. Opaque here, and meaningful only to the method that issued it.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>Who is working the door. Null when the attendee confirmed their own arrival.</summary>
    public Guid? RecordedByUserId { get; init; }

    /// <summary>How many of the party actually arrived, when the door counted.</summary>
    public int? ArrivedPartySize { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
