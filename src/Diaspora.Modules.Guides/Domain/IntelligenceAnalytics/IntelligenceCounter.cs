namespace Diaspora.Modules.Guides.Domain.IntelligenceAnalytics;

/// <summary>
/// How many times one thing happened on one country's intelligence surface.
/// <para>
/// <b>A key and a number, and the poverty of the shape is deliberate</b> — the same discipline
/// B8.1's cross-vertical counter uses. It holds no notice identifier, no reader, no session. The
/// question this table answers is "is the emergency page being used in Mauritius", and the moment it
/// could answer "by whom" it would be a behavioural log of people reading about domestic violence
/// hotlines, which is not a thing this platform should be able to produce.
/// </para>
/// <para>
/// <b>The kind is an open string.</b> A new signal is a new constant and a row that appears the first
/// time it fires — no migration, no enum member, no coordinated deploy. Counters that nothing has
/// reported simply do not exist, and a read of one returns zero.
/// </para>
/// </summary>
public sealed class IntelligenceCounter
{
    private IntelligenceCounter(string kind) => Kind = kind;

    private IntelligenceCounter() { }

    /// <summary>A lower-case signal name — <c>notice-viewed</c>. Half of the row's identity.</summary>
    public string Kind { get; private set; } = string.Empty;

    /// <summary>
    /// How many times it happened.
    /// <para>
    /// A <c>long</c> rather than an <c>int</c>, unlike B8.1's inventory counters, because this counts
    /// reads rather than rows. A single critical alert on a large country plausibly produces millions
    /// of views over its life, and a counter that silently wrapped at two billion would be discovered
    /// as a negative number on a dashboard.
    /// </para>
    /// </summary>
    public long Count { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    internal static IntelligenceCounter Start(string kind) => new(kind);

    /// <summary>Applies a delta, clamped at zero. Returns the value after the change.</summary>
    internal long Apply(long delta, DateTimeOffset at)
    {
        Count = Math.Max(0, Count + delta);
        UpdatedAt = at;

        return Count;
    }

    /// <summary>Replaces the value outright. For a rebuild from an authoritative count.</summary>
    internal long Set(long count, DateTimeOffset at)
    {
        Count = Math.Max(0, count);
        UpdatedAt = at;

        return Count;
    }
}
