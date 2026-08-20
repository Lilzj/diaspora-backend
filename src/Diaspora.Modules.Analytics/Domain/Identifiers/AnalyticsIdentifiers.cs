using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Analytics.Domain.Identifiers;

/// <summary>
/// One raw analytics fact.
/// <para>
/// <b>Server-assigned, never client-supplied.</b> A client that could choose an event identifier
/// could choose a colliding one and silently suppress somebody else's row, or choose a fresh one
/// per retry and be counted twice. Deduplication is done on
/// <see cref="Events.AnalyticsEvent.DedupKey"/>, which the server derives from what the event
/// <em>is</em> rather than from what the caller called it.
/// </para>
/// </summary>
public sealed record AnalyticsEventId(Guid Value) : StronglyTypedId(Value)
{
    public static AnalyticsEventId New() => new(Guid.NewGuid());

    public static AnalyticsEventId From(Guid value) => new(value);
}

/// <summary>
/// One piece of content, as analytics knows it.
/// <para>
/// Its own identifier rather than the content's, because the same Guid can legitimately name a
/// guide and a listing in two modules that have never met. The dimension is keyed by the pair.
/// </para>
/// </summary>
public sealed record AnalyticsContentDimensionId(Guid Value) : StronglyTypedId(Value)
{
    public static AnalyticsContentDimensionId New() => new(Guid.NewGuid());

    public static AnalyticsContentDimensionId From(Guid value) => new(value);
}

/// <summary>One day's aggregate for one piece of content.</summary>
public sealed record ContentDailyRollupId(Guid Value) : StronglyTypedId(Value)
{
    public static ContentDailyRollupId New() => new(Guid.NewGuid());

    public static ContentDailyRollupId From(Guid value) => new(value);
}

/// <summary>One day's aggregate for one owner across everything they published.</summary>
public sealed record OwnerDailyRollupId(Guid Value) : StronglyTypedId(Value)
{
    public static OwnerDailyRollupId New() => new(Guid.NewGuid());

    public static OwnerDailyRollupId From(Guid value) => new(value);
}

/// <summary>One registered event contract at one version.</summary>
public sealed record AnalyticsEventSchemaId(Guid Value) : StronglyTypedId(Value)
{
    public static AnalyticsEventSchemaId New() => new(Guid.NewGuid());

    public static AnalyticsEventSchemaId From(Guid value) => new(value);
}

/// <summary>The projection's position in the raw stream. Exactly one row exists.</summary>
public sealed record AnalyticsCheckpointId(Guid Value) : StronglyTypedId(Value)
{
    public static AnalyticsCheckpointId New() => new(Guid.NewGuid());

    public static AnalyticsCheckpointId From(Guid value) => new(value);
}

/// <summary>One administrative rebuild of a bounded window.</summary>
public sealed record AnalyticsBackfillRunId(Guid Value) : StronglyTypedId(Value)
{
    public static AnalyticsBackfillRunId New() => new(Guid.NewGuid());

    public static AnalyticsBackfillRunId From(Guid value) => new(value);
}
