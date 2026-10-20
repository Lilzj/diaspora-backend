namespace Diaspora.Modules.GeographicIntelligence.Domain.Heat;

/// <summary>What a heat layer's value is, which decides where it comes from and how a cell is withheld.</summary>
public enum HeatLayerKind
{
    /// <summary>How many published things of one vertical are in a cell. B4's counters and floor.</summary>
    Count,

    /// <summary>The median monthly asking rent of the homes in a cell, from Housing's rent sample.</summary>
    RentMedian,

    /// <summary>A cell's median rent against its country's median, the cost-of-living index (100 = the same).</summary>
    RentIndex,

    /// <summary>Safety incidents counted from one named source. Never a score.</summary>
    SafetyCount
}

/// <summary>
/// A density layer the platform can actually compute, and the source behind it.
/// </summary>
/// <param name="Layer">The client's layer id, kebab-case, exactly as its union spells it.</param>
/// <param name="Kind">What the value is.</param>
/// <param name="ResourceType">The B4 counter key that answers a <see cref="HeatLayerKind.Count"/> layer.</param>
/// <param name="Unit">What the intensity measures, for the legend and the accessible summary.</param>
/// <param name="TimeBasis">
/// Which of a row's times a time range filters on: <c>published</c>, or <c>starts</c> for an event.
/// Police data is filtered by <c>month</c>, which the safety layer says itself.
/// </param>
public sealed record HeatLayerDefinition(
    string Layer, HeatLayerKind Kind, string? ResourceType, string Unit, string TimeBasis);

/// <summary>
/// Which of the client's eight density layers this platform has a real source for — all eight since
/// 21 Oct 2026.
/// <para>
/// <b>The three that were refused, and how each is answered now without inventing anything.</b>
/// <c>housing-price</c> is the median monthly asking rent of the homes in a cell, and
/// <c>cost-of-living</c> is that median against the country's — the same rent-only index, the same
/// arithmetic and the same 5 / 20 listing minimums the neighbourhood cost-of-living read publishes
/// (the user's decision of 16 Sep 2026: cost of living comes from Diaspora's own listings). Both are
/// values per cell rather than counts, which B4's counter registry could not give, so they read
/// Housing's <c>IRentStatistics</c> and land on B4's grid. <c>safety</c> is an intensity of
/// <em>counts</em> from one named source at a time — corroborated community reports, or data.police.uk
/// where it publishes — and never a score, never blended (the decision of the same day).
/// </para>
/// <para>
/// <c>jobs</c> is in this list because B21 registered the counter Jobs had never contributed.
/// </para>
/// </summary>
public static class HeatLayerCatalog
{
    /// <summary>The safety source read when none is named: the one that exists in every country.</summary>
    public const string CommunityReportsSource = "community-reports";

    /// <summary>Police-recorded crime from data.police.uk, where it publishes.</summary>
    public const string PoliceRecordedCrimeSource = "police-recorded-crime";

    /// <summary>
    /// Community activity is <c>updates</c> — what people are posting about a place — rather than
    /// a blend of several counters. A blended layer cannot be labelled with a unit, and a legend
    /// that cannot say what its numbers count is decoration.
    /// </summary>
    public static IReadOnlyList<HeatLayerDefinition> All { get; } =
    [
        new("community-activity", HeatLayerKind.Count, "updates", "updates", "published"),
        new("housing-availability", HeatLayerKind.Count, "housing", "listings", "published"),
        new("marketplace-activity", HeatLayerKind.Count, "marketplace", "listings", "published"),
        new("jobs", HeatLayerKind.Count, "jobs", "jobs", "published"),
        new("events", HeatLayerKind.Count, "events", "events", "starts"),
        new("housing-price", HeatLayerKind.RentMedian, null, "median monthly asking rent", "published"),
        new("cost-of-living", HeatLayerKind.RentIndex, null, "rent index (100 = the country's median rent)", "published"),
        new("safety", HeatLayerKind.SafetyCount, null, "reports", "published")
    ];

    /// <summary>The sources a safety layer may be read from, one at a time.</summary>
    public static IReadOnlyList<string> SafetySources { get; } =
        [CommunityReportsSource, PoliceRecordedCrimeSource];

    public static HeatLayerDefinition? Find(string? layer) =>
        string.IsNullOrWhiteSpace(layer)
            ? null
            : All.FirstOrDefault(definition =>
                string.Equals(definition.Layer, layer.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The supported layer ids, for the capability report and for an error message.</summary>
    public static IReadOnlyList<string> SupportedLayers { get; } =
        [.. All.Select(definition => definition.Layer)];
}

/// <summary>
/// The time ranges a heat layer may be narrowed to — exactly the client's <c>HeatMapTimeRange</c>
/// union (21 Oct 2026). Anything else is refused rather than read as "no filter", because a layer
/// labelled "last 24 hours" that was not filtered is a false statement on a map.
/// </summary>
public static class HeatTimeRange
{
    public static IReadOnlyDictionary<string, TimeSpan> All { get; } =
        new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
        {
            ["24h"] = TimeSpan.FromHours(24),
            ["7d"] = TimeSpan.FromDays(7),
            ["30d"] = TimeSpan.FromDays(30),
            ["90d"] = TimeSpan.FromDays(90)
        };

    /// <summary>The accepted values, in the order the client lists them.</summary>
    public static IReadOnlyList<string> Supported { get; } = ["24h", "7d", "30d", "90d"];

    /// <summary>The canonical spelling and its span, or null when the value is not one of the four.</summary>
    public static (string Name, TimeSpan Span)? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return All.TryGetValue(trimmed, out var span)
            ? (Supported.First(name => string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase)), span)
            : null;
    }
}
