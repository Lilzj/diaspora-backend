namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>
/// A page of attendees.
/// <para>
/// Carries no note and no decline reason. What somebody said when they withdrew is between them and
/// the organizer, and a guest list that leaked it would be read by everybody holding the link.
/// </para>
/// </summary>
public sealed record AttendeePageResponse
{
    public IReadOnlyList<AttendeeResponse> Items { get; init; } = [];

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    public required int GoingCount { get; init; }

    public int? RemainingPlaces { get; init; }

    /// <summary>
    /// Whether the door is open right now.
    /// <para>
    /// **Stated rather than left to be re-derived.** The rule is
    /// <c>CheckInOpensAt ?? StartsAt - 1h</c> through <c>EndsAt + 12h</c>, and a client that
    /// reimplemented it would drift from it — so the screen offering a Check in button was
    /// offering one the platform would refuse, with no way to know in advance.
    /// </para>
    /// </summary>
    public required bool AcceptsCheckIn { get; init; }

    /// <summary>
    /// When the door opens, resolved.
    /// <para>
    /// The event's own <c>CheckInOpensAt</c> where it has one, and an hour before the start where
    /// it does not — so a caller can say <em>when</em> rather than only <em>not yet</em>.
    /// </para>
    /// </summary>
    public required DateTimeOffset CheckInOpensAt { get; init; }
}
