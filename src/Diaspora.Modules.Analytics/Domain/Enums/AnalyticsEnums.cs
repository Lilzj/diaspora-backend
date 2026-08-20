namespace Diaspora.Modules.Analytics.Domain.Enums;

/// <summary>
/// What a piece of analytics is about.
/// <para>
/// The frontend's <c>AnalyticsSubjectType</c>, narrowed to the members this phase serves. Members
/// the frontend models and this deployment does not answer for are absent rather than present and
/// empty: a subject type that exists in the enum and returns nothing is indistinguishable from a
/// subject with no activity, and the two are very different claims.
/// </para>
/// <para>
/// <b><c>Country</c> and <c>Community</c> would be separate members and neither is here.</b> Phase
/// 13 spent a phase separating them and Phase 15 encodes the separation in its own union; adding
/// one of them to this enum before the aggregates exist would invite the conflation back in.
/// </para>
/// </summary>
public enum AnalyticsSubjectType
{
    /// <summary>One creator, always the caller. There is no "another creator" subject.</summary>
    Creator = 1,

    /// <summary>One addressable piece of content, keyed by <see cref="AnalyticsContentType"/>.</summary>
    Content = 2
}

/// <summary>
/// The kinds of content analytics attributes activity to.
/// <para>
/// <b>Deliberately the same vocabulary B6 already publishes.</b> Every engagement contract this
/// module consumes — reaction, bookmark, share, comment — carries B6's <c>ContentType</c> string,
/// and a second vocabulary here would mean a translation table that silently drops a member B6
/// added. The names are B6's, lower-cased on the wire.
/// </para>
/// <para>
/// <c>Unknown</c> exists for the same reason B6's <c>Other</c> does: a later module can publish an
/// engagement against a content type this enum has not heard of, and the honest answer is to store
/// the row under a type that says so rather than to discard the fact or guess at it.
/// </para>
/// </summary>
public enum AnalyticsContentType
{
    Unknown = 0,

    /// <summary>A community post. The client calls this an Update.</summary>
    Post = 1,

    /// <summary>A long-form guide, owned by B8.</summary>
    Guide = 3,

    /// <summary>
    /// A service provider profile.
    /// <para>
    /// B6 calls this member <c>Listing</c> and the client calls it <c>Provider</c>; Phase 15 uses
    /// <c>listing</c> for a <em>marketplace</em> listing, which is a different thing entirely. The
    /// member is named for what it is and <c>AnalyticsContentTypes</c> owns both translations, so
    /// the collision is resolved in one file rather than in every mapper.
    /// </para>
    /// </summary>
    Provider = 4,

    Job = 5,

    Housing = 6,

    /// <summary>A marketplace listing.</summary>
    Marketplace = 7,

    Event = 8,

    Organization = 9
}

/// <summary>
/// Where a fact came from, and therefore how much weight it can bear.
/// <para>
/// <b>This is the single most important field on an analytics row.</b> §41 of the phase brief
/// forbids a client freely incrementing a business metric, and the enforcement is not a check
/// somewhere — it is that every row records which class produced it, every metric declares which
/// classes it counts, and the two are joined at query time. A deployment that later decides
/// client-attested views are untrustworthy excludes them with a predicate rather than a migration.
/// </para>
/// </summary>
public enum AnalyticsTrustClass
{
    /// <summary>
    /// Derived from a published integration event. The producing module committed the fact inside
    /// its own transaction and the platform delivered it; no client had a say.
    /// </summary>
    Server = 1,

    /// <summary>
    /// Submitted by an authenticated client through the telemetry contract. Attributed to the
    /// caller by the server — the client sends no identifier — rate-limited, allow-listed, and
    /// deduplicated so a member cannot count themselves twice.
    /// </summary>
    Client = 2
}

/// <summary>
/// How a metric should be rendered. Drives formatting, never meaning.
/// <para>The frontend's <c>AnalyticsMetricFormat</c>, and the narrower Phase 7 <c>format</c>.</para>
/// </summary>
public enum AnalyticsMetricFormat
{
    Count = 1,
    Percentage = 2,
    Currency = 3,
    Duration = 4
}

/// <summary>
/// How sensitive a metric is.
/// <para>
/// Carried on the definition rather than decided per screen, so a new endpoint cannot publish an
/// owner-only figure by forgetting.
/// </para>
/// </summary>
public enum AnalyticsPrivacyClass
{
    /// <summary>Safe for anybody who can see the subject.</summary>
    Public = 1,

    /// <summary>The subject's own operator only — a creator's own reach.</summary>
    Owner = 2,

    /// <summary>Platform administrators.</summary>
    Administrative = 3,

    /// <summary>Money. Owner or platform administration, never a third party.</summary>
    Financial = 4
}

/// <summary>How a number was arrived at — the frontend's second provenance axis.</summary>
public enum AnalyticsComputation
{
    /// <summary>The server aggregated it from stored facts. The only kind that is authoritative.</summary>
    ServerAggregate = 1,

    /// <summary>The server modelled rather than counted it.</summary>
    Estimated = 2
}

/// <summary>
/// The periods this deployment serves.
/// <para>
/// The Phase 7 set exactly — <c>today</c>, <c>7d</c>, <c>30d</c>, <c>90d</c>, <c>year</c>,
/// <c>custom</c> — because §15 of that register lists six and the client offers six. The Phase 15
/// model has eleven presets and treats which of them a subject supports as a <em>capability</em>
/// rather than a fact about the union, which is why the extra five are absent here and reported
/// through <c>supportedRanges</c> instead of guessed at.
/// </para>
/// </summary>
public enum AnalyticsPeriodPreset
{
    Today = 1,
    Last7Days = 2,
    Last30Days = 3,
    Last90Days = 4,
    Year = 5,
    Custom = 6
}

/// <summary>How finely a series is bucketed. The server decides; the client never regroups.</summary>
public enum AnalyticsGranularity
{
    Hour = 1,
    Day = 2,
    Week = 3,
    Month = 4
}

/// <summary>
/// Why a comparison could not be made.
/// <para>
/// §7 of the Phase 15 register forbids showing a misleading percentage when there is no valid
/// baseline, and this is how that is said rather than by rendering <c>0%</c>. A dashboard reading
/// "+0%" and one reading "no activity to compare against" are two different claims and only one of
/// them is true.
/// </para>
/// </summary>
public enum AnalyticsComparisonUnavailableReason
{
    /// <summary>The subject did not exist for the whole baseline window.</summary>
    InsufficientHistory = 1,

    /// <summary>The baseline window had no activity at all, so a ratio is undefined.</summary>
    ZeroBaseline = 2,

    /// <summary>This deployment does not compute this baseline for this metric.</summary>
    Unsupported = 3
}

/// <summary>The lifecycle of an administrative backfill.</summary>
public enum AnalyticsBackfillStatus
{
    Queued = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}
