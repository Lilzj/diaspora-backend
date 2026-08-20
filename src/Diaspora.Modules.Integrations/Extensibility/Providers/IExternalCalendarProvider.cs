namespace Diaspora.Modules.Integrations.Extensibility.Providers;

/// <summary>
/// A calendar service, behind one interface.
/// <para>
/// <b>Every method takes an access token and returns an outcome.</b> The token arrives already
/// unsealed and already refreshed — that is the calling service's job, done once, rather than six
/// adapters each getting the refresh-on-401 dance subtly different.
/// </para>
/// <para>
/// <b>The capability set is declared, not assumed.</b> "Do not pretend Apple Calendar has the same
/// API semantics as Google Calendar if it does not" — so an adapter states what it can do, the
/// capability report serves that statement, and a call into something it cannot do returns
/// <see cref="Diaspora.Modules.Integrations.Domain.Enums.IntegrationFailureKind.Unsupported"/> rather
/// than a generic failure. A client reading the report and a client making the call get the same
/// answer.
/// </para>
/// <para>
/// <b>Attendees appear nowhere in this interface.</b> Not as a parameter, not on the draft, not on
/// the result. "Exporting an event must not disclose who else is going to a third party the other
/// attendees never agreed to" — and a field that does not exist cannot be populated by a later
/// change that did not read this paragraph.
/// </para>
/// </summary>
public interface IExternalCalendarProvider
{
    /// <summary>The dotted provider key — <c>calendar.google</c>.</summary>
    string Key { get; }

    /// <summary>Whether this deployment has what the adapter needs.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// What this provider can actually do.
    /// <para>
    /// Empty for the not-configured default, rather than a list of things it cannot serve — the same
    /// rule B21's routing provider follows, and for the same reason: a client reading the capability
    /// report must get the same answer as one calling the route.
    /// </para>
    /// </summary>
    IReadOnlyCollection<ExternalCalendarCapability> Capabilities { get; }

    /// <summary>
    /// The calendars this grant can see.
    /// <para>
    /// <b><see cref="ExternalCalendar.AllowsModifications"/> must reflect the provider's real access
    /// role.</b> Assuming true produces a write that fails at the provider after the UI has already
    /// offered the button — and on a shared calendar, a member who believes an event was added.
    /// </para>
    /// </summary>
    Task<IntegrationOutcome<IReadOnlyList<ExternalCalendar>>> ListCalendarsAsync(
        string accessToken, CancellationToken cancellationToken = default);

    /// <summary>Writes a new entry.</summary>
    Task<IntegrationOutcome<ExternalCalendarEvent>> CreateEventAsync(
        string accessToken,
        string calendarId,
        ExternalCalendarEventDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces an entry this platform wrote.</summary>
    Task<IntegrationOutcome<ExternalCalendarEvent>> UpdateEventAsync(
        string accessToken,
        string calendarId,
        string externalEventId,
        ExternalCalendarEventDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an entry.
    /// <para>
    /// An entry the provider no longer has is a success, not a failure: the member deleted it
    /// themselves, and reporting that as an error would put a red banner in front of somebody who
    /// did exactly what they meant to.
    /// </para>
    /// </summary>
    Task<IntegrationOutcome<IntegrationVoid>> DeleteEventAsync(
        string accessToken,
        string calendarId,
        string externalEventId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one entry back, to confirm it is still there.
    /// <para>
    /// What lets a sync distinguish "the member deleted this" from "we never wrote it", which are
    /// the two states a re-export has to tell apart. A provider that cannot do this returns
    /// <see cref="Diaspora.Modules.Integrations.Domain.Enums.IntegrationFailureKind.Unsupported"/>
    /// and the sync degrades to export-only rather than deleting anything on a guess.
    /// </para>
    /// </summary>
    Task<IntegrationOutcome<ExternalCalendarEvent?>> FindEventAsync(
        string accessToken,
        string calendarId,
        string externalEventId,
        CancellationToken cancellationToken = default);
}

/// <summary>What a calendar adapter can do. Declared per adapter; served by the capability report.</summary>
public enum ExternalCalendarCapability
{
    List = 0,
    CreateEvent = 1,
    UpdateEvent = 2,
    DeleteEvent = 3,

    /// <summary>Reading an entry back. Not the same as importing a member's whole calendar.</summary>
    ReadEvent = 4,

    /// <summary>
    /// Reading entries the member did not export.
    /// <para>
    /// The direction the register singles out: "import is the direction that matters. Reading a
    /// member's calendar brings personal events into Diaspora's storage." Declared separately from
    /// <see cref="ReadEvent"/> so an adapter can offer the narrow one without the broad one.
    /// </para>
    /// </summary>
    ImportEvents = 5,

    /// <summary>A push channel the provider will call back on. Needed before "synced" is claimable.</summary>
    WatchChannel = 6
}

/// <summary>A calendar the grant can see.</summary>
/// <param name="Id">Provider-scoped. A name, never a capability — access is re-derived per call.</param>
/// <param name="Title">
/// Personal. "Therapy", "Dad's chemo". Never logged, never in an analytics event, never cached
/// beyond the request.
/// </param>
/// <param name="AllowsModifications">The provider's real access role. See the interface summary.</param>
/// <param name="TimeZone">IANA. The calendar's own zone, which need not be the device's.</param>
public sealed record ExternalCalendar(
    string Id,
    string Title,
    bool AllowsModifications,
    bool IsPrimary,
    string? Color = null,
    string? TimeZone = null);

/// <summary>
/// An entry on its way into a calendar.
/// <para>
/// <b><see cref="TimeZone"/> is required and is never defaulted to the server's.</b> "A community
/// meeting in Lagos is at 18:00 WAT whether or not the member is currently in London, and a calendar
/// entry that says 17:00 BST is a different — and wrong — claim about a real-world event." Making
/// the field required is what stops the conversion happening by omission.
/// </para>
/// </summary>
public sealed record ExternalCalendarEventDraft
{
    public required string Title { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    /// <summary>IANA. Required — see the type summary.</summary>
    public required string TimeZone { get; init; }

    public required bool IsAllDay { get; init; }

    public string? Location { get; init; }

    public string? Notes { get; init; }

    public string? Url { get; init; }

    /// <summary>Minutes before the start. Empty means "use the calendar's default".</summary>
    public IReadOnlyList<int> ReminderMinutes { get; init; } = [];

    /// <summary>RFC 5545 RRULE. Null for a single occurrence.</summary>
    public string? RecurrenceRule { get; init; }

    /// <summary>
    /// The idempotency key the provider should see, where it supports one.
    /// <para>
    /// An adapter whose provider has no such concept ignores it, and the platform's own
    /// source-to-external mapping is what makes the write idempotent regardless — two barriers,
    /// because only one of them is under this platform's control.
    /// </para>
    /// </summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>What a provider gives back once an entry exists.</summary>
public sealed record ExternalCalendarEvent(
    string ExternalEventId,
    string CalendarId,
    DateTimeOffset WrittenAt,
    string? ViewUrl = null);
