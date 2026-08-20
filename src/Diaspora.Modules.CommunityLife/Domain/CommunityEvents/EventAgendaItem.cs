using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.CommunityEvents;

/// <summary>
/// One item on an event's programme.
/// <para>
/// <b>An offset from the start rather than a wall-clock time</b>, which is what keeps the programme
/// correct when an organizer moves the event. A conference rescheduled by two hours should not need
/// its fourteen agenda rows retyped, and a programme stored as absolute times would.
/// </para>
/// </summary>
public sealed class EventAgendaItem : Entity<Guid>
{
    private EventAgendaItem(
        Guid id, string title, string? description, int startOffsetMinutes, int? durationMinutes,
        string? speakerName, int order) : base(id)
    {
        Title = title;
        Description = description;
        StartOffsetMinutes = startOffsetMinutes;
        DurationMinutes = durationMinutes;
        SpeakerName = speakerName;
        Order = order;
    }

    private EventAgendaItem() { }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Minutes after the event's own start time. Moves with the event.</summary>
    public int StartOffsetMinutes { get; private set; }

    public int? DurationMinutes { get; private set; }

    /// <summary>Who is doing it. Free text — most agenda items name somebody with no account here.</summary>
    public string? SpeakerName { get; private set; }

    public int Order { get; private set; }

    /// <summary>The item's real start, given when the event actually begins.</summary>
    public DateTimeOffset StartsAt(DateTimeOffset eventStart) =>
        eventStart.AddMinutes(StartOffsetMinutes);

    public static EventAgendaItem Create(
        Guid id,
        string? title,
        string? description = null,
        int startOffsetMinutes = 0,
        int? durationMinutes = null,
        string? speakerName = null,
        int order = 0) =>
        new(
            id,
            Clip(title, CommunityLifeLimits.MaxAgendaTitle) ?? string.Empty,
            Clip(description, CommunityLifeLimits.MaxNoteLength),
            Math.Max(0, startOffsetMinutes),
            durationMinutes is { } duration && duration > 0 ? duration : null,
            Clip(speakerName, CommunityLifeLimits.MaxDisplayName),
            Math.Max(0, order));

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
