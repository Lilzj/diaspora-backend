namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>One row on a community calendar. Not necessarily an event.</summary>
public sealed record CalendarEntryResponse
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public required bool IsAllDay { get; init; }

    /// <summary><c>Event</c>, <c>Holiday</c>, or whatever a contributed source calls its rows.</summary>
    public required string EntryType { get; init; }

    public string? Description { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>Set when the entry is one of ours, so a client can deep-link to it.</summary>
    public Guid? EventId { get; init; }

    /// <summary>Which source contributed it.</summary>
    public required string SourceKey { get; init; }
}
