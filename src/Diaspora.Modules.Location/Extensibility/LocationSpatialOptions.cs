namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// Which spatial and geocoding implementations this deployment runs.
/// <para>
/// Bound from <c>Location:Spatial</c>. Both keys are empty by default, which selects the only
/// registered provider — so a deployment that adds nothing gets the in-process relational
/// implementation and never has to know this section exists.
/// </para>
/// </summary>
public sealed class LocationSpatialOptions
{
    public const string SectionName = "Location:Spatial";

    /// <summary>
    /// The <see cref="Domain.Spatial.IGeospatialQueryProvider.Key"/> to use. Empty means the
    /// single registered provider.
    /// </summary>
    public string? QueryProvider { get; set; }

    /// <summary>
    /// The <see cref="Domain.Spatial.IGeocodingProvider.Key"/> to use. Empty means the single
    /// registered provider.
    /// </summary>
    public string? GeocodingProvider { get; set; }

    /// <summary>
    /// Whether to run the shipped seed data at startup.
    /// <para>
    /// On by default because an empty catalogue makes every location screen in the app render
    /// its empty state, which reads as a broken deployment rather than an unseeded one. A
    /// deployment with a real gazetteer import turns it off.
    /// </para>
    /// </summary>
    public bool SeedCatalogOnStartup { get; set; } = true;

    /// <summary>
    /// How long a nearby provider gets before the aggregator gives up on it.
    /// <para>
    /// The aggregate endpoint is only as fast as its slowest contributor, and a vertical
    /// having a bad day must not hold the other five. Its group comes back empty and the rest
    /// of the response is unaffected.
    /// </para>
    /// </summary>
    public int NearbyProviderTimeoutMilliseconds { get; set; } = 2500;

    /// <summary>The same budget, for resource counters on a hub screen.</summary>
    public int ResourceCounterTimeoutMilliseconds { get; set; } = 1500;

    /// <summary>
    /// The floor a map cluster's total must reach before the cell is returned.
    /// <para>
    /// One by default, and the default is a claim worth stating: every counter that ships
    /// aggregates <b>published content</b> — a job, a listing, an event — and every row behind a
    /// count is already fetchable one at a time from its own module's search route, so a cluster
    /// of one is not a disclosure and suppressing it would hide real inventory from somebody
    /// looking for somewhere to live.
    /// </para>
    /// <para>
    /// <b>Raise it before shipping a counter that aggregates people, not after.</b> A density
    /// layer over members turns a small cell into an identifier, and two suppressed cells either
    /// side of a published total make the suppressed values recoverable by subtraction. The
    /// response publishes whatever this is set to, so a client can say what it is not being shown.
    /// </para>
    /// </summary>
    public int MinimumClusterCellCount { get; set; } = 1;
}
