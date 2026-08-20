namespace Diaspora.Modules.GeographicIntelligence.Domain.Heat;

/// <summary>
/// A density layer the platform can actually compute, and the resource counter behind it.
/// </summary>
/// <param name="Layer">The client's layer id, kebab-case, exactly as its union spells it.</param>
/// <param name="ResourceType">The B4 counter key that answers it.</param>
/// <param name="Unit">What the intensity counts, for the legend and the accessible summary.</param>
public sealed record HeatLayerDefinition(string Layer, string ResourceType, string Unit);

/// <summary>
/// Which of the client's eight density layers this platform has a real source for.
/// <para>
/// <b>Five of the eight, and the three that are missing are missing for two different reasons that
/// are both worth stating.</b> <c>housing-price</c> and <c>cost-of-living</c> would need a value
/// per cell and B4's counter registry answers only "how many"; a price layer derived from a count
/// would be a chart of where listings are, labelled as a chart of what they cost. <c>safety</c>
/// has no source anywhere on the platform. An unsupported layer is refused with a named code
/// rather than answered with an empty set, because an empty heat layer renders as a quiet
/// neighbourhood.
/// </para>
/// <para>
/// <c>jobs</c> is in this list because B21 registered the counter Jobs had never contributed. It
/// was the one vertical of seven with no <c>ILocationResourceCounter</c>, so a jobs layer and a
/// jobs score dimension had no source at all — not because the data was missing, but because
/// nobody had wired the seam.
/// </para>
/// </summary>
public static class HeatLayerCatalog
{
    /// <summary>
    /// Community activity is <c>updates</c> — what people are posting about a place — rather than
    /// a blend of several counters. A blended layer cannot be labelled with a unit, and a legend
    /// that cannot say what its numbers count is decoration.
    /// </summary>
    public static IReadOnlyList<HeatLayerDefinition> All { get; } =
    [
        new("community-activity", "updates", "updates"),
        new("housing-availability", "housing", "listings"),
        new("marketplace-activity", "marketplace", "listings"),
        new("jobs", "jobs", "jobs"),
        new("events", "events", "events")
    ];

    public static HeatLayerDefinition? Find(string? layer) =>
        string.IsNullOrWhiteSpace(layer)
            ? null
            : All.FirstOrDefault(definition =>
                string.Equals(definition.Layer, layer.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The supported layer ids, for the capability report and for an error message.</summary>
    public static IReadOnlyList<string> SupportedLayers { get; } =
        [.. All.Select(definition => definition.Layer)];
}
