using System.ComponentModel.DataAnnotations;

namespace Diaspora.Modules.Analytics.Options;

/// <summary>
/// What a deployment can change about analytics without a release.
/// <para>
/// <b>Every default here is a decision, not a placeholder</b>, and the two that matter most are the
/// retention windows: the register asks for raw events for 90 days and rollups for 25 months, and
/// the 25 is chosen so a year-on-year comparison has a complete baseline rather than a truncated
/// one.
/// </para>
/// </summary>
public sealed class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    /// <summary>
    /// The timezone every calendar day in this deployment's analytics is cut in.
    /// <para>
    /// <b>UTC by default, and stated on every response rather than assumed.</b> A day boundary has
    /// to be somewhere; putting it in UTC and saying so is honest, and putting it in the server's
    /// local time and saying nothing is how a dashboard silently changes shape when the host moves
    /// region. §10 of the phase brief forbids the second outright.
    /// </para>
    /// </summary>
    [Required]
    public string ReportingTimeZone { get; set; } = "UTC";

    /// <summary>How long raw analytics rows are kept. The recomputation and backfill horizon.</summary>
    [Range(1, 3650)]
    public int RawEventRetentionDays { get; set; } = 90;

    /// <summary>
    /// How long daily rollups are kept.
    /// <para>
    /// Twenty-five months, so a comparison against the same month last year has a whole month to
    /// compare against rather than the part of it that survived.
    /// </para>
    /// </summary>
    [Range(1, 3650)]
    public int RollupRetentionDays { get; set; } = 762;

    /// <summary>The widest custom period a caller may ask for. Bounded, per §25.</summary>
    [Range(1, 3650)]
    public int MaximumCustomRangeDays { get; set; } = 366;

    /// <summary>The most events one telemetry request may carry.</summary>
    [Range(1, 500)]
    public int MaximumBatchSize { get; set; } = 50;

    /// <summary>
    /// How far in the past a submitted <c>occurredAt</c> may be before it is refused.
    /// <para>
    /// A buffered client event can legitimately be hours old — the client's ring buffer holds two
    /// hundred records and a member can be offline — so the window is generous. It is not unbounded,
    /// because an unbounded window lets a caller write into a period that has already been reported
    /// on.
    /// </para>
    /// </summary>
    [Range(1, 168)]
    public int MaximumClientEventAgeHours { get; set; } = 48;

    /// <summary>
    /// How far in the future a submitted <c>occurredAt</c> may be before it is clamped.
    /// <para>
    /// Small, and clamped rather than refused: a device with a slightly wrong clock is an ordinary
    /// event and dropping its telemetry would quietly under-count exactly the members whose devices
    /// are least well looked after.
    /// </para>
    /// </summary>
    [Range(1, 1440)]
    public int MaximumClientEventSkewMinutes { get; set; } = 15;

    /// <summary>How many raw rows the projection folds in per pass.</summary>
    [Range(10, 10000)]
    public int ProjectionBatchSize { get; set; } = 500;

    /// <summary>How often the projection runs when the sweep is enabled.</summary>
    [Range(5, 3600)]
    public int ProjectionIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Whether the projection runs on a timer in this process.
    /// <para>
    /// DEBT-049's lesson applied on the module's first day: a background loop querying the
    /// integration suite's one shared SQLite connection races every test in it, and a test asserting
    /// on a figure after a timer happens to have fired is a flaky test wearing a passing badge. The
    /// projection exposes a public <c>RunOnceAsync</c> and the tests drive that.
    /// </para>
    /// </summary>
    public bool EnableProjectionSweep { get; set; } = true;

    /// <summary>Whether the retention sweep runs on a timer in this process. Same reason.</summary>
    public bool EnableRetentionSweep { get; set; } = true;

    /// <summary>How often the retention sweep runs.</summary>
    [Range(60, 86400)]
    public int RetentionIntervalSeconds { get; set; } = 3600;

    /// <summary>How many rows one retention pass deletes before yielding.</summary>
    [Range(10, 100000)]
    public int RetentionBatchSize { get; set; } = 1000;

    /// <summary>The widest window one backfill request may cover.</summary>
    [Range(1, 3650)]
    public int MaximumBackfillDays { get; set; } = 92;

    /// <summary>How many days one backfill pass rebuilds before yielding.</summary>
    [Range(1, 365)]
    public int BackfillSliceDays { get; set; } = 7;

    /// <summary>
    /// The key the actor hash is derived with.
    /// <para>
    /// <b>A deployment secret, and the hash is only as pseudonymous as this is.</b> An unkeyed hash
    /// of a Guid is reversible by anybody who can enumerate Guids they already hold — which is
    /// every operator with a user table — so the key is what makes the analytics store unable to
    /// name a reader on its own. The default is a fixed development string and the startup
    /// validator refuses it outside Development.
    /// </para>
    /// </summary>
    [Required]
    public string ActorHashKey { get; set; } = DevelopmentActorHashKey;

    /// <summary>The default above, named so the validator can recognise it.</summary>
    public const string DevelopmentActorHashKey = "diaspora-development-analytics-actor-key";

    /// <summary>
    /// How long a served figure may be cached.
    /// <para>
    /// Short. The projection runs every thirty seconds, so a five-minute cache would make
    /// <c>dataAsOf</c> a claim about the cache rather than about the data — and §13 of the phase
    /// brief forbids reporting a freshness the backend does not know.
    /// </para>
    /// </summary>
    [Range(0, 3600)]
    public int QueryCacheSeconds { get; set; } = 60;
}
