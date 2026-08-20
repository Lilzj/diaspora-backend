namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>
/// A page of one series' dates.
/// <para>
/// The cursor is the sequence number rather than a timestamp, which is why this page has no tie
/// group to carry: within a series the sequence is unique and orders identically to the start time,
/// even when two dates have been moved onto the same evening.
/// </para>
/// </summary>
public sealed record EventOccurrencePageResponse
{
    public required Guid SeriesId { get; init; }

    public IReadOnlyList<EventOccurrenceResponse> Items { get; init; } = [];

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}
