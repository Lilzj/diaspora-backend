using System.ComponentModel.DataAnnotations;

namespace Diaspora.Modules.GeographicIntelligence.Options;

/// <summary>
/// How this deployment reads data.police.uk for the safety counts.
/// <para>
/// No credential: the API is open and needs none. Every setting bounds what one member's request can
/// cost the source, which asks callers to stay under fifteen requests a second.
/// </para>
/// </summary>
public sealed class PoliceUkOptions
{
    public const string SectionName = "GeographicIntelligence:PoliceUk";

    /// <summary>
    /// Whether the source is read at all. Off in the test host, which must never call a third party;
    /// on by default everywhere else.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The API root, ending in a slash.</summary>
    [Required]
    public string BaseAddress { get; set; } = "https://data.police.uk/api/";

    /// <summary>How many of the most recent published months are counted.</summary>
    [Range(1, 36)]
    public int Months { get; set; } = 12;

    /// <summary>How long one request to the source may take before the answer is "could not be read".</summary>
    [Range(1, 60)]
    public int RequestTimeoutSeconds { get; set; } = 10;

    /// <summary>How many requests one count makes at once. Well under the source's per-second limit.</summary>
    [Range(1, 10)]
    public int MaxConcurrentRequests { get; set; } = 4;

    /// <summary>
    /// How long a month's counts for a point are kept. A published month is rarely revised, so this is
    /// long; the list of published months is refreshed far more often.
    /// </summary>
    [Range(1, 2160)]
    public int CacheHours { get; set; } = 168;

    /// <summary>
    /// The widest viewport, in degrees of latitude, the safety heat layer reads from this source
    /// (21 Oct 2026). The source refuses an area holding more than 10,000 crimes in a month, and a
    /// city viewport much wider than this does; a narrower cap also keeps one pan to a few requests.
    /// </summary>
    [Range(0.01, 2)]
    public double MaximumHeatSpanDegrees { get; set; } = 0.25d;
}
