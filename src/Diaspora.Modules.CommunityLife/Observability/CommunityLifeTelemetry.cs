using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.CommunityLife.Observability;

/// <summary>
/// The module's activity source and meters.
/// <para>
/// <b>The counters are chosen to answer questions about the platform as a community, not about the
/// process as a program.</b> Requests per second and error rates are the host's job. What this
/// module can uniquely report is whether people are actually meeting — and the most revealing
/// pairing is responses against check-ins. A deployment where those diverge is one where members
/// say they will come and do not, which is invisible from any infrastructure dashboard and is the
/// earliest signal that an events platform has become a noticeboard.
/// </para>
/// </summary>
public static class CommunityLifeTelemetry
{
    /// <summary>The name the observability contributor subscribes the host's tracer to.</summary>
    public const string SourceName = "Diaspora.CommunityLife";

    public static ActivitySource Source { get; } = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    /// <summary>Organizations published. Tagged by country and type.</summary>
    public static Counter<long> OrganizationsPublished { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.organizations.published", "organization",
            "Organizations published.");

    /// <summary>Members admitted. Tagged by whether the organization approves requests.</summary>
    public static Counter<long> MembersJoined { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.members.joined", "member", "Members admitted.");

    /// <summary>
    /// Members who stopped belonging, tagged by whether they left or were removed. Compared
    /// against joins, the module's churn signal.
    /// </summary>
    public static Counter<long> MembersLeft { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.members.left", "member",
            "Members who left, were removed or lapsed.");

    /// <summary>Events published. Tagged by country and category.</summary>
    public static Counter<long> EventsPublished { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.events.published", "event", "Events published.");

    /// <summary>
    /// Events called off, tagged by whether anybody had said they were coming.
    /// The number that tells an operator whether the platform is advertising things that do not
    /// happen.
    /// </summary>
    public static Counter<long> EventsCancelled { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.events.cancelled", "event", "Events cancelled.");

    /// <summary>Events that ran to completion.</summary>
    public static Counter<long> EventsCompleted { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.events.completed", "event", "Events completed.");

    /// <summary>Responses recorded, tagged by status.</summary>
    public static Counter<long> RsvpsSubmitted { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.rsvps.submitted", "rsvp", "Responses recorded.");

    /// <summary>Responses withdrawn, tagged by decline reason.</summary>
    public static Counter<long> RsvpsWithdrawn { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.rsvps.withdrawn", "rsvp", "Responses withdrawn.");

    /// <summary>
    /// Members through the door, tagged by check-in method. Paired with responses, the module's
    /// clearest measure of whether a community actually turns up.
    /// </summary>
    public static Counter<long> CheckIns { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.checkins", "checkin", "Members recorded as present.");

    /// <summary>Commitments nobody honoured. The other half of the pairing above.</summary>
    public static Counter<long> NoShows { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.no-shows", "rsvp",
            "Commitments that ended without an attendance.");

    /// <summary>
    /// Responses refused because the last place had gone.
    /// <para>
    /// The contention meter. While it stays near zero the optimistic-concurrency guard on the
    /// capacity counters is adequate; if it climbs, the platform's guarded atomic counter — B10's
    /// outstanding recommendation, still outstanding — has become a requirement rather than a
    /// suggestion.
    /// </para>
    /// </summary>
    public static Counter<long> CapacityRacesLost { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.capacity.races-lost", "attempt",
            "Responses refused because no places remained.");

    /// <summary>
    /// Occurrences written by the expansion service.
    /// <para>
    /// The number that says whether recurring events are actually being kept ahead of the clock. It
    /// should track the number of live series times their frequency; a flat line while series exist
    /// means the sweep has stopped, and the symptom a member sees is a weekly meetup whose calendar
    /// simply ends.
    /// </para>
    /// </summary>
    public static Counter<long> OccurrencesMaterialised { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.occurrences.materialised", "occurrence",
            "Recurring-event occurrences written ahead of the clock.");

    /// <summary>Series published. Tagged by frequency.</summary>
    public static Counter<long> SeriesPublished { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.series.published", "series",
            "Recurring events published.");

    /// <summary>Events and organizations reported, tagged by subject and reason.</summary>
    public static Counter<long> SubjectsReported { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.reports", "report",
            "Events and organizations reported.");

    /// <summary>Spam signals raised, tagged by severity and source.</summary>
    public static Counter<long> SpamSignalsRaised { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.spam.signals", "signal", "Spam signals raised.");

    /// <summary>Searches run. Tagged by whether a term was supplied and which engine answered.</summary>
    public static Counter<long> SearchesRun { get; } =
        Meter.CreateCounter<long>(
            "diaspora.community-life.searches", "search",
            "Community Life searches executed.");

    /// <summary>Hours from publication to an event filling. Null-heavy: most never fill.</summary>
    public static Histogram<double> TimeToFillHours { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.community-life.time-to-fill.hours", "h",
            "Hours from publication to an event reaching capacity.");

    /// <summary>Attendance rate at completion, nought to one. The module's success measure.</summary>
    public static Histogram<double> AttendanceRate { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.community-life.attendance.rate", "ratio",
            "Commitments that turned up, at event completion.");
}
