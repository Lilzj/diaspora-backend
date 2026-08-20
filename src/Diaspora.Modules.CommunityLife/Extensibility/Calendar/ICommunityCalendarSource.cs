namespace Diaspora.Modules.CommunityLife.Extensibility.Calendar;

/// <summary>
/// How something other than an event gets onto a community calendar.
/// <para>
/// <b>The seam behind the brief's "neighborhood calendar" requirement.</b> The module contributes
/// its own events through one implementation of this interface; national holidays, religious
/// observances and school terms are further implementations in whichever assembly knows about them.
/// A deployment gains Ramadan dates by shipping a class.
/// </para>
/// <para>
/// <b>Calendar-provider integration is explicitly out of scope</b>, and this seam is how it stays
/// out while remaining reachable: a Google or Outlook sync is a source like any other, and lands
/// without a change here.
/// </para>
/// <para>
/// <b>Contract.</b> Return quickly or return nothing — sources are fanned out concurrently and a
/// slow one delays the whole calendar. Throwing costs that source's rows and nobody else's.
/// </para>
/// </summary>
public interface ICommunityCalendarSource
{
    /// <summary>The key this source's rows appear under. Unique across the registered set.</summary>
    string SourceKey { get; }

    /// <summary>Whether this source has anything for the window. A cheap synchronous check.</summary>
    bool AppliesTo(CalendarWindow window) => true;

    Task<IReadOnlyList<CalendarEntry>> GetEntriesAsync(
        CalendarWindow window, CancellationToken cancellationToken = default);
}
