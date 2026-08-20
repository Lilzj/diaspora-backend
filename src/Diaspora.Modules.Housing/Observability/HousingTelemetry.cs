using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.Housing.Observability;

/// <summary>
/// The module's activity source and meters.
/// <para>
/// <b>The counters are chosen to answer questions about the platform as a relocation service, not
/// about the process as a program.</b> Requests per second and error rates are the host's job.
/// What this module can uniquely report is whether people are finding homes — and the single most
/// revealing pairing is viewings requested against viewings attended. A deployment where those
/// diverge is one where landlords are not turning up, and that is invisible from any
/// infrastructure dashboard while being the earliest signal of rental fraud the platform has.
/// </para>
/// </summary>
public static class HousingTelemetry
{
    /// <summary>The name the observability contributor subscribes the host's tracer to.</summary>
    public const string SourceName = "Diaspora.Housing";

    public static ActivitySource Source { get; } = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    /// <summary>Properties published. Tagged by country and accommodation type.</summary>
    public static Counter<long> ListingsPublished { get; } =
        Meter.CreateCounter<long>(
            "diaspora.housing.listings.published", "listing", "Properties published.");

    /// <summary>Viewing requests made.</summary>
    public static Counter<long> ViewingsRequested { get; } =
        Meter.CreateCounter<long>(
            "diaspora.housing.viewings.requested", "viewing", "Viewing requests made.");

    /// <summary>Viewings the landlord agreed a time for.</summary>
    public static Counter<long> ViewingsScheduled { get; } =
        Meter.CreateCounter<long>(
            "diaspora.housing.viewings.scheduled", "viewing", "Viewings scheduled.");

    /// <summary>Viewings that happened.</summary>
    public static Counter<long> ViewingsCompleted { get; } =
        Meter.CreateCounter<long>(
            "diaspora.housing.viewings.completed", "viewing", "Viewings completed.");

    /// <summary>
    /// Viewings nobody attended, tagged by which party failed to appear. Compared against
    /// completions, this is the module's earliest fraud signal and its clearest measure of whether
    /// members' time is being wasted.
    /// </summary>
    public static Counter<long> ViewingNoShows { get; } =
        Meter.CreateCounter<long>(
            "diaspora.housing.viewings.no-shows", "viewing", "Viewings nobody attended.");

    /// <summary>Reservations opened.</summary>
    public static Counter<long> ReservationsCreated { get; } =
        Meter.CreateCounter<long>(
            "diaspora.housing.reservations.created", "reservation", "Reservations created.");

    /// <summary>
    /// Reservations that ended without anybody moving in, tagged by whether a person or the clock
    /// ended it. How much of the platform's housing stock is being held by people who went quiet.
    /// </summary>
    public static Counter<long> ReservationsAbandoned { get; } =
        Meter.CreateCounter<long>(
            "diaspora.housing.reservations.abandoned", "reservation",
            "Reservations cancelled, declined or expired.");

    /// <summary>Members who moved in. The module's success counter.</summary>
    public static Counter<long> Occupations { get; } =
        Meter.CreateCounter<long>(
            "diaspora.housing.occupations", "let", "Members who moved into a property.");

    /// <summary>
    /// Reservation attempts refused because the last unit had gone.
    /// <para>
    /// The contention meter, and the number that would justify implementing B10's guarded atomic
    /// counter. While it stays near zero the optimistic-concurrency guard is adequate; if it climbs,
    /// the platform recommendation has become a platform requirement.
    /// </para>
    /// </summary>
    public static Counter<long> ReservationRacesLost { get; } =
        Meter.CreateCounter<long>(
            "diaspora.housing.reservations.races-lost", "attempt",
            "Reservation attempts refused because no units remained.");

    /// <summary>Listings reported, tagged by reason. The safety surface's front line.</summary>
    public static Counter<long> ListingsReported { get; } =
        Meter.CreateCounter<long>(
            "diaspora.housing.listings.reported", "report", "Listings reported.");

    /// <summary>Listings held or flagged by a fraud signal, tagged by severity and source.</summary>
    public static Counter<long> FraudSignalsRaised { get; } =
        Meter.CreateCounter<long>(
            "diaspora.housing.fraud.signals", "signal", "Fraud signals raised.");

    /// <summary>Neighbourhood insights submitted, tagged by category.</summary>
    public static Counter<long> InsightsSubmitted { get; } =
        Meter.CreateCounter<long>(
            "diaspora.housing.insights.submitted", "insight",
            "Neighbourhood insights submitted.");

    /// <summary>Insights a moderator published.</summary>
    public static Counter<long> InsightsApproved { get; } =
        Meter.CreateCounter<long>(
            "diaspora.housing.insights.approved", "insight",
            "Neighbourhood insights approved.");

    /// <summary>Searches run. Tagged by whether a term was supplied and which engine answered.</summary>
    public static Counter<long> SearchesRun { get; } =
        Meter.CreateCounter<long>(
            "diaspora.housing.searches", "search", "Housing searches executed.");

    /// <summary>Days from publication to somebody moving in.</summary>
    public static Histogram<double> TimeToLetDays { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.housing.time-to-let.days", "d",
            "Days from publication to first occupation.");

    /// <summary>Hours from a viewing request to the landlord answering it.</summary>
    public static Histogram<double> ViewingResponseHours { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.housing.viewings.response.hours", "h",
            "Hours from a viewing request to a landlord answering.");
}
