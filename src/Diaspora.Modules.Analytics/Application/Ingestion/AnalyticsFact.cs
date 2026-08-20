using Diaspora.Modules.Analytics.Domain.Enums;

namespace Diaspora.Modules.Analytics.Application.Ingestion;

/// <summary>
/// One fact offered to ingestion, before it becomes a row.
/// <para>
/// <b>One shape for both paths.</b> A client submission and a consumer of a published contract
/// arrive as the same record, so the deduplication, the attribution, the clamping and the counters
/// are written once. Two ingestion paths with two validators is how a rule ends up enforced on one
/// of them.
/// </para>
/// </summary>
/// <param name="EventType">A registered event name.</param>
/// <param name="Version">Which schema version the caller wrote against.</param>
/// <param name="SourceModule">Which module's contract produced it, or <c>client</c>.</param>
/// <param name="OccurredAt">When the fact happened, as its producer reported it.</param>
/// <param name="ActorUserId">Who did it. Hashed before storage and never persisted raw.</param>
/// <param name="ContentType">What it was about.</param>
/// <param name="ContentId">Which item.</param>
/// <param name="OwnerUserId">
/// Whose content it is, when the producer already knows. Server contracts usually carry it; a
/// client never does, and ingestion resolves it from the dimension.
/// </param>
/// <param name="SubjectUserId">
/// Who the fact is <em>about</em>, when it is about a person rather than an item — a follow, a
/// profile view. Distinct from the actor: one is the follower, the other the followed.
/// </param>
/// <param name="CountryCode">Where, as of the event.</param>
/// <param name="CommunityId">Which community, as of the event.</param>
/// <param name="Surface">Which surface it happened on. Low cardinality.</param>
/// <param name="Qualifier">A low-cardinality qualifier the metric may filter on.</param>
/// <param name="Weight">What this occurrence counts as. One, except where a producer reports a batch.</param>
/// <param name="SourceEventId">
/// The producing contract's own identifier. The deduplication identity for every server-derived
/// fact, because it is stable across retries and replays — which is exactly what the platform's
/// consumer guidance says to key on.
/// </param>
/// <param name="CorrelationId">The request that produced the fact.</param>
public sealed record AnalyticsFact(
    string EventType,
    int Version,
    string SourceModule,
    DateTimeOffset OccurredAt,
    Guid? ActorUserId = null,
    AnalyticsContentType ContentType = AnalyticsContentType.Unknown,
    Guid? ContentId = null,
    Guid? OwnerUserId = null,
    Guid? SubjectUserId = null,
    string? CountryCode = null,
    Guid? CommunityId = null,
    string? Surface = null,
    string? Qualifier = null,
    int Weight = 1,
    Guid? SourceEventId = null,
    Guid? CorrelationId = null);

/// <summary>What ingestion did with one fact.</summary>
public enum AnalyticsIngestionOutcome
{
    /// <summary>Stored.</summary>
    Accepted = 1,

    /// <summary>Already stored. Not a failure — the platform delivers at least once.</summary>
    Duplicate = 2,

    /// <summary>Refused. <see cref="AnalyticsIngestionResult.Reason"/> says why.</summary>
    Rejected = 3
}

/// <summary>The answer for one fact.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Reason">The refusal code when rejected. Null otherwise.</param>
public sealed record AnalyticsIngestionResult(AnalyticsIngestionOutcome Outcome, string? Reason = null)
{
    public static AnalyticsIngestionResult Accepted { get; } = new(AnalyticsIngestionOutcome.Accepted);

    public static AnalyticsIngestionResult Duplicate { get; } = new(AnalyticsIngestionOutcome.Duplicate);

    public static AnalyticsIngestionResult Rejected(string reason) =>
        new(AnalyticsIngestionOutcome.Rejected, reason);
}
