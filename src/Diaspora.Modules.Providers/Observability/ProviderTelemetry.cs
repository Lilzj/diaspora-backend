using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.Providers.Observability;

/// <summary>
/// The module's live instruments.
/// <para>
/// Declared alongside the contributor that subscribes to them, and both are required — declaring
/// a source name with nothing behind it subscribes the tracer to silence, which is the mistake
/// the module extension guide names and which B2 actually made.
/// </para>
/// <para>
/// The instruments answer the questions this module will be asked in production, and they are
/// not the obvious ones. "How many providers" is a vanity metric that a database query answers
/// better. <see cref="DiscoveryLatency"/> tagged by sort order tells you that <em>distance</em>
/// sorting is slow, which points straight at the bounding-box prefilter and is the only version
/// of "search is slow" anybody can act on. <see cref="EmptyResults"/> rising is the single best
/// early signal that a category tree or a radius default is wrong, and no error rate would ever
/// show it, because returning nothing is a perfectly successful response.
/// <see cref="UnverifiedContactAttempts"/> is the number that says whether verification is worth
/// what it costs.
/// </para>
/// </summary>
public static class ProviderTelemetry
{
    public const string SourceName = "Diaspora.Providers";

    public const string Version = "1.0.0";

    public static readonly ActivitySource Activity = new(SourceName, Version);

    public static readonly Meter Meter = new(SourceName, Version);

    /// <summary>Providers registered, tagged by type. Shows which kinds actually sign up.</summary>
    public static readonly Counter<long> ProvidersRegistered =
        Meter.CreateCounter<long>(
            "diaspora.providers.registered",
            unit: "{provider}",
            description: "Providers created, tagged by provider type.");

    /// <summary>Providers published, tagged by type and category. The funnel's second half.</summary>
    public static readonly Counter<long> ProvidersPublished =
        Meter.CreateCounter<long>(
            "diaspora.providers.published",
            unit: "{provider}",
            description: "Providers that became discoverable, tagged by type and category.");

    /// <summary>Discovery pages served, tagged by sort order and whether the scope was spatial.</summary>
    public static readonly Counter<long> DiscoveryQueries =
        Meter.CreateCounter<long>(
            "diaspora.providers.discovery_queries",
            unit: "{query}",
            description: "Discovery pages served, tagged by sort and scope.");

    /// <summary>
    /// Discovery latency, tagged by sort order.
    /// <para>
    /// Tagged rather than aggregate, because "search is slow" is unactionable and "distance sort
    /// is slow" points at the bounding-box prefilter.
    /// </para>
    /// </summary>
    public static readonly Histogram<double> DiscoveryLatency =
        Meter.CreateHistogram<double>(
            "diaspora.providers.discovery_latency",
            unit: "ms",
            description: "Time to serve one discovery page.");

    /// <summary>
    /// Discovery queries that matched nothing, tagged by whether a category and a radius were set.
    /// <para>
    /// A successful response with an empty body, so no error rate will ever surface it. A rising
    /// count is the earliest signal that a category is missing from the tree, that a default
    /// radius is too tight, or that a country has no providers in it yet.
    /// </para>
    /// </summary>
    public static readonly Counter<long> EmptyResults =
        Meter.CreateCounter<long>(
            "diaspora.providers.empty_results",
            unit: "{query}",
            description: "Discovery queries that returned nothing.");

    /// <summary>Contact actions, tagged by method. What an owner's dashboard is really about.</summary>
    public static readonly Counter<long> ContactActions =
        Meter.CreateCounter<long>(
            "diaspora.providers.contact_actions",
            unit: "{action}",
            description: "Calls, messages and directions taps, tagged by contact method.");

    /// <summary>
    /// Contact actions against providers that are not verified.
    /// <para>
    /// The number that says whether verification is worth what it costs members to obtain. If
    /// nearly every contact goes to an unverified provider, the badge is not driving behaviour
    /// and the programme needs rethinking rather than more staff.
    /// </para>
    /// </summary>
    public static readonly Counter<long> UnverifiedContactAttempts =
        Meter.CreateCounter<long>(
            "diaspora.providers.unverified_contact_attempts",
            unit: "{action}",
            description: "Contact actions against providers holding no current verification.");

    /// <summary>Verification submissions, tagged by kind.</summary>
    public static readonly Counter<long> VerificationsSubmitted =
        Meter.CreateCounter<long>(
            "diaspora.providers.verifications_submitted",
            unit: "{verification}",
            description: "Verification claims submitted, tagged by kind.");

    /// <summary>Verification outcomes, tagged by kind and state. Grants, refusals, lapses.</summary>
    public static readonly Counter<long> VerificationOutcomes =
        Meter.CreateCounter<long>(
            "diaspora.providers.verification_outcomes",
            unit: "{verification}",
            description: "Verification outcomes applied, tagged by kind and state.");

    /// <summary>
    /// A registered verification provider threw or failed, tagged by module.
    /// <para>
    /// Non-zero means an integration is degraded while the platform keeps working, which is
    /// exactly the condition that otherwise goes unnoticed until somebody asks why nothing has
    /// been auto-verified for a fortnight.
    /// </para>
    /// </summary>
    public static readonly Counter<long> VerificationProviderFailures =
        Meter.CreateCounter<long>(
            "diaspora.providers.verification_provider_failures",
            unit: "{failure}",
            description: "Verification providers that failed and were fallen through.");

    /// <summary>A registered availability provider threw, tagged by module.</summary>
    public static readonly Counter<long> AvailabilityProviderFailures =
        Meter.CreateCounter<long>(
            "diaspora.providers.availability_provider_failures",
            unit: "{failure}",
            description: "Availability providers that failed and were fallen through.");

    /// <summary>Reviews written, tagged by rating. The distribution, live.</summary>
    public static readonly Counter<long> ReviewsSubmitted =
        Meter.CreateCounter<long>(
            "diaspora.providers.reviews_submitted",
            unit: "{review}",
            description: "Reviews written, tagged by rating.");

    /// <summary>Review summaries recomputed. Should track review writes closely; a gap is drift.</summary>
    public static readonly Counter<long> ReviewSummariesRecomputed =
        Meter.CreateCounter<long>(
            "diaspora.providers.review_summaries_recomputed",
            unit: "{summary}",
            description: "Rating rollups recomputed from the reviews table.");

    /// <summary>Media claims that failed, leaving an asset at risk of being swept by B5.</summary>
    public static readonly Counter<long> MediaClaimFailures =
        Meter.CreateCounter<long>(
            "diaspora.providers.media_claim_failures",
            unit: "{claim}",
            description: "Media claims that failed. Each one is an asset B5 will sweep.");

    /// <summary>Search projections generated, for B15's eventual ingestion.</summary>
    public static readonly Counter<long> SearchProjectionsGenerated =
        Meter.CreateCounter<long>(
            "diaspora.providers.search_projections_generated",
            unit: "{projection}",
            description: "Provider search projections generated.");
}
