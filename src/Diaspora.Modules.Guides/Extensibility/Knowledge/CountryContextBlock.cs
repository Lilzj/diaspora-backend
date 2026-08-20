namespace Diaspora.Modules.Guides.Extensibility.Knowledge;

/// <summary>
/// One contextual band on a country dashboard — "142 jobs", "38 homes", "9 events this week".
/// <para>
/// <b>A label, a number and a route.</b> The route is a relative API path the client follows to
/// the vertical that owns the content, which is what keeps the dashboard a directory rather than a
/// second front page: B8.1 says how much there is and where to go, and the Jobs module answers what
/// the jobs are, with its own visibility rules, its own moderation and its own freshness.
/// </para>
/// <para>
/// <b>No content, ever.</b> A block that carried three job titles would be a cache of somebody
/// else's data, rendered without their rules, going stale on a schedule nobody owns. The one number
/// it does carry is the one thing that is cheap to keep roughly right and useless to get exactly
/// right — nobody has ever cared whether it was 142 or 143.
/// </para>
/// </summary>
public sealed record CountryContextBlock
{
    /// <summary>The counter kind — <c>jobs</c>, <c>housing</c>. Clients switch on it for an icon.</summary>
    public required string Kind { get; init; }

    /// <summary>The band's heading, as the contributing module wants it written.</summary>
    public required string Label { get; init; }

    public required int Count { get; init; }

    /// <summary>
    /// Where to send somebody who taps it — a relative API path on the owning module, or null when
    /// the contributor would rather the client decide.
    /// </summary>
    public string? Route { get; init; }

    /// <summary>Where the band sits. Lower renders first.</summary>
    public int Order { get; init; }
}
