namespace Diaspora.Modules.GeographicIntelligence.Domain.Scores;

/// <summary>
/// The nine score dimensions, spelled exactly as the client's union type spells them.
/// <para>
/// <b>These strings are the contract.</b> The frontend's <c>ScoreDimension.id</c> is a closed
/// union of nine kebab-case literals and a tenth value would fail its type check rather than
/// render as unknown. They are constants here so a rename is a compile error in the one place it
/// can be one, and so nothing composes a dimension id out of an enum name — <c>CostOfLiving</c>
/// serialises to <c>CostOfLiving</c>, which is not <c>cost-of-living</c>, and that class of
/// mismatch has cost this platform a phase before.
/// </para>
/// </summary>
public static class ScoreDimensionId
{
    /// <summary>Nothing on this platform computes a safety score. Present for the day one does.</summary>
    public const string Safety = "safety";

    public const string CostOfLiving = "cost-of-living";

    public const string CommunityActivity = "community-activity";

    public const string Housing = "housing";

    public const string Transport = "transport";

    public const string Services = "services";

    public const string Events = "events";

    public const string Jobs = "jobs";

    public const string Amenities = "amenities";

    /// <summary>Every dimension the client can render, in the order a breakdown reads best.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Safety, CostOfLiving, CommunityActivity, Housing,
        Transport, Services, Events, Jobs, Amenities
    ];
}

/// <summary>
/// Why a dimension is not in the breakdown.
/// <para>
/// <b>The field that lets the client stop simulating.</b> Until now a missing dimension and a
/// dimension nobody has assessed looked identical from the wire, so the frontend filled both with
/// an estimate and marked it. These reasons are different products: <see cref="NoDataSource"/> is
/// a commercial decision nobody has taken, <see cref="NotAssessed"/> is a place nobody has
/// described yet and is fixed by a contributor, and <see cref="InsufficientPopulation"/> is a
/// statistic this platform refuses to publish because it would be noise.
/// </para>
/// </summary>
public enum DimensionUnavailableReason
{
    /// <summary>
    /// No authoritative source for this dimension exists anywhere on the platform, and none is
    /// contracted. Safety and amenities are both here, and neither is fixable by writing code.
    /// </summary>
    NoDataSource = 1,

    /// <summary>
    /// A source exists and nobody has filled it in for this place. A moderated neighbourhood
    /// profile with the band left <c>Unknown</c>. Fixed by a contributor, not by a vendor.
    /// </summary>
    NotAssessed = 2,

    /// <summary>
    /// The dimension is a rank, and there are too few comparable places to rank against.
    /// <para>
    /// A percentile over three neighbourhoods is noise wearing a number's clothes, and this
    /// platform publishes the refusal rather than the noise. The floor is configuration.
    /// </para>
    /// </summary>
    InsufficientPopulation = 3,

    /// <summary>
    /// The vertical that would answer this could not be reached, or has no counter registered at
    /// all. Distinct from a zero: "there are no jobs here" and "nobody could tell us about jobs"
    /// are different sentences and only one of them is about the neighbourhood.
    /// </summary>
    SourceUnavailable = 4,

    /// <summary>
    /// The platform holds a real figure for this and no scale on which to express it as a score.
    /// <para>
    /// Jobs, events and services are all here. "Fourteen jobs are open in this neighbourhood" is
    /// true and published — it is in <c>resourceCounts</c> on the same response — but turning it
    /// into 0–100 needs either a ceiling nobody measured or a comparison population this platform
    /// cannot assemble cheaply. The count is the honest artefact and it is not withheld; only the
    /// score is.
    /// </para>
    /// </summary>
    NoComparableScale = 5
}
