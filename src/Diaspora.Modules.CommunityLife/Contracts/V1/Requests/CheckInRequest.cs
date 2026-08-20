namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>
/// Record somebody as present.
/// <para>
/// <see cref="Token"/> is why the request carries more than an identifier. A manual check-in
/// identifies the attendee directly; a scanned one identifies them by whatever was on the badge.
/// Carrying an opaque token alongside is what lets a QR method land later without changing this
/// shape — and no registered method reads it today, because QR infrastructure is out of scope.
/// </para>
/// </summary>
public sealed record CheckInRequest
{
    /// <summary>The attendee. Omit for a token-only arrival.</summary>
    public Guid? AttendeeUserId { get; init; }

    /// <summary>The response being checked in, when the caller has it.</summary>
    public Guid? RsvpId { get; init; }

    /// <summary>Whatever the door presented. Opaque, and meaningful only to the method that issued it.</summary>
    public string? Token { get; init; }

    /// <summary><c>Manual</c>, <c>Organizer</c>, <c>SelfConfirmed</c>.</summary>
    public string? Method { get; init; }

    /// <summary>How many of the party actually arrived, when the door counted.</summary>
    public int? ArrivedPartySize { get; init; }
}
