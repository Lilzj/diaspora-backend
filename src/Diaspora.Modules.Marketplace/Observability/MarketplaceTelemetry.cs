using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.Marketplace.Observability;

/// <summary>
/// The module's activity source and meters.
/// <para>
/// <b>The counters are chosen to answer questions about the platform as a marketplace, not about
/// the process as a program.</b> Requests per second and error rates are the host's job. What this
/// module can uniquely report is whether things are being listed, whether they are selling, and
/// whether the people who reserve items actually turn up — and a deployment where reservations
/// rise while completions do not is one where buyers are wasting sellers' Saturdays, which is
/// invisible from any infrastructure dashboard.
/// </para>
/// </summary>
public static class MarketplaceTelemetry
{
    /// <summary>The name the observability contributor subscribes the host's tracer to.</summary>
    public const string SourceName = "Diaspora.Marketplace";

    public static ActivitySource Source { get; } = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    /// <summary>Listings published. Tagged by country and category.</summary>
    public static Counter<long> ListingsPublished { get; } =
        Meter.CreateCounter<long>(
            "diaspora.marketplace.listings.published", "listing", "Listings published.");

    /// <summary>Units sold. Tagged by country and category.</summary>
    public static Counter<long> ItemsSold { get; } =
        Meter.CreateCounter<long>("diaspora.marketplace.items.sold", "item", "Units sold.");

    /// <summary>Negotiations opened.</summary>
    public static Counter<long> OffersMade { get; } =
        Meter.CreateCounter<long>(
            "diaspora.marketplace.offers.made", "offer", "Negotiations opened.");

    /// <summary>Negotiations that ended in agreement.</summary>
    public static Counter<long> OffersAccepted { get; } =
        Meter.CreateCounter<long>(
            "diaspora.marketplace.offers.accepted", "offer", "Negotiations accepted.");

    /// <summary>Reservations created.</summary>
    public static Counter<long> ReservationsCreated { get; } =
        Meter.CreateCounter<long>(
            "diaspora.marketplace.reservations.created", "reservation", "Reservations created.");

    /// <summary>
    /// Reservations that ended without a hand-off, tagged by whether a person or the clock ended
    /// it. Compared against completions, this is how much of the marketplace's stock is being held
    /// hostage by people who do not turn up.
    /// </summary>
    public static Counter<long> ReservationsAbandoned { get; } =
        Meter.CreateCounter<long>(
            "diaspora.marketplace.reservations.abandoned", "reservation",
            "Reservations cancelled or expired.");

    /// <summary>Reservations that completed.</summary>
    public static Counter<long> ReservationsCompleted { get; } =
        Meter.CreateCounter<long>(
            "diaspora.marketplace.reservations.completed", "reservation",
            "Reservations completed.");

    /// <summary>Listings reported, tagged by reason. The safety surface's front line.</summary>
    public static Counter<long> ListingsReported { get; } =
        Meter.CreateCounter<long>(
            "diaspora.marketplace.listings.reported", "report", "Listings reported.");

    /// <summary>Listings held or flagged by a fraud signal, tagged by severity and source.</summary>
    public static Counter<long> FraudSignalsRaised { get; } =
        Meter.CreateCounter<long>(
            "diaspora.marketplace.fraud.signals", "signal", "Fraud signals raised.");

    /// <summary>Searches run. Tagged by whether a term was supplied.</summary>
    public static Counter<long> SearchesRun { get; } =
        Meter.CreateCounter<long>(
            "diaspora.marketplace.searches", "search", "Marketplace searches executed.");

    /// <summary>Days from publication to first sale.</summary>
    public static Histogram<double> TimeToSaleDays { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.marketplace.time-to-sale.days", "d",
            "Days from publication to first sale.");

    /// <summary>Hours a negotiation ran before it settled.</summary>
    public static Histogram<double> NegotiationHours { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.marketplace.negotiation.hours", "h",
            "Hours from first offer to settlement.");
}
