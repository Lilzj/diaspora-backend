using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Extensibility.Calendar;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Calendar;

/// <summary>
/// B12's own events, as calendar entries.
/// <para>
/// <b>The module is one source among several, and registers itself the same way anything else
/// would.</b> A deployment adding public holidays, a consulate's opening times or a national day
/// feed gets them beside the events with no change here — and could remove this one without
/// breaking the calendar.
/// </para>
/// <para>
/// <b>Public events only, and no viewer audience is applied.</b> A calendar is a shared surface: a
/// members-only gathering appearing on it would reach exactly the people its organizer excluded.
/// </para>
/// </summary>
public sealed class CommunityEventCalendarSource : ICommunityCalendarSource
{
    private const int WindowLimit = 500;

    private readonly ICommunityEventRepository _events;

    public CommunityEventCalendarSource(ICommunityEventRepository events) => _events = events;

    public string SourceKey => "community-events";

    public async Task<IReadOnlyList<CalendarEntry>> GetEntriesAsync(
        CalendarWindow window, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);

        var events = await _events.FindInWindowAsync(
            window.From,
            window.To,
            window.CountryCode,
            window.NeighborhoodAreaId,
            WindowLimit,
            cancellationToken);

        return
        [
            .. events.Select(communityEvent => new CalendarEntry
            {
                Id = communityEvent.Id.Value.ToString(),
                Title = communityEvent.Title.Value,
                StartsAt = communityEvent.Schedule.StartsAt,
                EndsAt = communityEvent.Schedule.EndsAt,

                // An event that spans a day or more reads better as an all-day entry than as a
                // block running off the bottom of every column it crosses.
                IsAllDay = communityEvent.Schedule.Duration >= TimeSpan.FromHours(20),
                EntryType = communityEvent.CategoryKey,
                Description = communityEvent.Description.Summary,
                CountryCode = communityEvent.Location.CountryCode,
                EventId = communityEvent.Id.Value
            })
        ];
    }
}
