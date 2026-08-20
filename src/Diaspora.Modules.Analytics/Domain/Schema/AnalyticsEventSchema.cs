using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Analytics.Domain.Schema;

/// <summary>
/// One registered event contract, at one version.
/// <para>
/// <b>FRONTEND-PHASE15-GAP-029 asks for this by name</b> — "a schema registry, so an event the
/// client emits and the backend does not know about is rejected <em>visibly</em> rather than
/// silently dropped". A silently dropped event is the worst outcome available: the client believes
/// it is instrumented, the dashboard reads zero, and the two agree for months.
/// </para>
/// <para>
/// <b>Reference data, seeded, and versioned by (name, version).</b> A schema change is a new row
/// rather than an edit, so a row already in the raw store still names a contract that exists and a
/// three-month-old chart can still say which definition produced it. §6 of the phase brief: "A
/// future event schema must be able to evolve without silently corrupting historical analytics."
/// </para>
/// <para>
/// <b><see cref="AllowsClientSubmission"/> is the anti-manipulation boundary.</b> A client posting
/// <c>content.saved</c> would be incrementing a business metric the owning module already commits
/// and publishes; the registry refuses it with a named code rather than a 400 that reads like a
/// bug. §41 exactly.
/// </para>
/// </summary>
public sealed class AnalyticsEventSchema : AggregateRoot<AnalyticsEventSchemaId>
{
    private AnalyticsEventSchema(
        AnalyticsEventSchemaId id,
        string eventType,
        int version,
        AnalyticsTrustClass trustClass,
        bool allowsClientSubmission,
        AnalyticsDedupStrategy dedupStrategy,
        bool requiresContent,
        string description) : base(id)
    {
        EventType = eventType;
        Version = version;
        TrustClass = trustClass;
        AllowsClientSubmission = allowsClientSubmission;
        DedupStrategy = dedupStrategy;
        RequiresContent = requiresContent;
        Description = description;
    }

    private AnalyticsEventSchema() { }

    public string EventType { get; private set; } = string.Empty;

    public int Version { get; private set; } = 1;

    /// <summary>Which class rows written under this schema belong to.</summary>
    public AnalyticsTrustClass TrustClass { get; private set; }

    /// <summary>Whether the telemetry endpoint will accept this event from a client.</summary>
    public bool AllowsClientSubmission { get; private set; }

    public AnalyticsDedupStrategy DedupStrategy { get; private set; }

    /// <summary>Whether a submission without a content reference is malformed.</summary>
    public bool RequiresContent { get; private set; }

    /// <summary>What the event means, for the operator reading the registry.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Whether this schema is still accepted.
    /// <para>
    /// Retired rather than deleted, so history keeps naming a contract that exists — the same rule
    /// ADR-020 states for superseded abstractions. A retired schema refuses new submissions and
    /// still explains old rows.
    /// </para>
    /// </summary>
    public bool IsAccepted { get; private set; } = true;

    public static AnalyticsEventSchema Register(
        AnalyticsEventSchemaId id,
        string eventType,
        int version,
        AnalyticsTrustClass trustClass,
        bool allowsClientSubmission,
        AnalyticsDedupStrategy dedupStrategy,
        bool requiresContent,
        string description)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new AnalyticsEventSchema(
            id,
            eventType.Trim(),
            Math.Max(1, version),
            trustClass,
            allowsClientSubmission,
            dedupStrategy,
            requiresContent,
            description.Trim());
    }

    public void Retire() => IsAccepted = false;

    public void Reinstate() => IsAccepted = true;
}

/// <summary>
/// How a schema's deduplication identity is composed.
/// <para>
/// <b>The strategy is part of the contract because it is part of the metric's definition.</b> "One
/// person counted once per item per day" is not an implementation choice about views; it is what
/// the published definition of a view says, and putting it on the schema is what stops the two
/// drifting apart.
/// </para>
/// </summary>
public enum AnalyticsDedupStrategy
{
    /// <summary>
    /// Event type, content, actor and calendar day.
    /// <para>
    /// A second view by the same person of the same item on the same day is not a second view. This
    /// is what makes a client unable to inflate its own creator's figures by reloading a page.
    /// </para>
    /// </summary>
    PerActorPerContentPerDay = 1,

    /// <summary>
    /// Event type and the producing event's identifier.
    /// <para>
    /// For server-derived rows: the published contract's <c>EventId</c> is stable across retries and
    /// replays, which is exactly what the platform's own consumer guidance says to deduplicate on.
    /// </para>
    /// </summary>
    PerSourceEvent = 2,

    /// <summary>
    /// Event type, actor, subject and calendar day, without a content reference.
    /// <para>
    /// For facts about a person rather than an item — a profile view, a follow.
    /// </para>
    /// </summary>
    PerActorPerSubjectPerDay = 3
}
