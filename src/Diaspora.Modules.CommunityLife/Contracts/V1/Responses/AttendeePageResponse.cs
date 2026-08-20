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
}
